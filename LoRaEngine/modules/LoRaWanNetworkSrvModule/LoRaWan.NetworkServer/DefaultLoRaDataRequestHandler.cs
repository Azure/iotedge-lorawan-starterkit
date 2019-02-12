// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DefaultLoRaDataRequestHandler : ILoRaDataRequestHandler
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private readonly ILoRaPayloadDecoder payloadDecoder;

        public DefaultLoRaDataRequestHandler(
            NetworkServerConfiguration configuration,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            ILoRaPayloadDecoder payloadDecoder)
        {
            this.configuration = configuration;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
            this.payloadDecoder = payloadDecoder;
        }

        public async Task<LoRaDeviceRequestProcessResult> ProcessRequestAsync(LoRaRequest request, LoRaDevice loRaDevice)
        {
            var timeWatcher = new LoRaOperationTimeWatcher(request.LoRaRegion, request.StartTime);
            var loraPayload = (LoRaPayloadData)request.Payload;
            var frameCounterStrategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);
            if (frameCounterStrategy == null)
            {
                Logger.Log(loRaDevice.DevEUI, $"failed to resolve frame count update strategy, device gateway: {loRaDevice.GatewayID}, message ignored", LogLevel.Error);
                return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.ApplicationError);
            }

            var payloadFcnt = loraPayload.GetFcnt();
            var requiresConfirmation = loraPayload.IsConfirmed();

            using (new LoRaDeviceFrameCounterSession(loRaDevice, frameCounterStrategy))
            {
                // Leaf devices that restart lose the counter. In relax mode we accept the incoming frame counter
                // ABP device does not reset the Fcnt so in relax mode we should reset for 0 (LMIC based) or 1
                var isFrameCounterFromNewlyStartedDevice = false;
                if (payloadFcnt <= 1)
                {
                    if (loRaDevice.IsABP)
                    {
                        if (loRaDevice.IsABPRelaxedFrameCounter && loRaDevice.FCntUp >= 0 && payloadFcnt <= 1)
                        {
                            // known problem when device restarts, starts fcnt from zero
                            _ = frameCounterStrategy.ResetAsync(loRaDevice);
                            isFrameCounterFromNewlyStartedDevice = true;
                        }
                    }
                    else if (loRaDevice.FCntUp == payloadFcnt && payloadFcnt == 0)
                    {
                        // Some devices start with frame count 0
                        isFrameCounterFromNewlyStartedDevice = true;
                    }
                }

                // Reply attack or confirmed reply
                // Confirmed resubmit: A confirmed message that was received previously but we did not answer in time
                // Device will send it again and we just need to return an ack (but also check for C2D to send it over)
                var isConfirmedResubmit = false;
                if (!isFrameCounterFromNewlyStartedDevice && payloadFcnt <= loRaDevice.FCntUp)
                {
                    // if it is confirmed most probably we did not ack in time before or device lost the ack packet so we should continue but not send the msg to iothub
                    if (requiresConfirmation && payloadFcnt == loRaDevice.FCntUp)
                    {
                        if (!loRaDevice.ValidateConfirmResubmit(payloadFcnt))
                        {
                            Logger.Log(loRaDevice.DevEUI, $"resubmit from confirmed message exceeds threshold of {LoRaDevice.MaxConfirmationResubmitCount}, message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Debug);
                            return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.ConfirmationResubmitThresholdExceeded);
                        }

                        isConfirmedResubmit = true;
                        Logger.Log(loRaDevice.DevEUI, $"resubmit from confirmed message detected, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);
                    }
                    else
                    {
                        Logger.Log(loRaDevice.DevEUI, $"invalid frame counter, message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);
                        return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.InvalidFrameCounter);
                    }
                }

                var fcntDown = 0;
                // If it is confirmed it require us to update the frame counter down
                // Multiple gateways: in redis, otherwise in device twin
                if (requiresConfirmation)
                {
                    fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, payloadFcnt);

                    // Failed to update the fcnt down
                    // In multi gateway scenarios it means the another gateway was faster than using, can stop now
                    if (fcntDown <= 0)
                    {
                        // update our fcntup anyway?
                        // loRaDevice.SetFcntUp(payloadFcnt);
                        Logger.Log(loRaDevice.DevEUI, "another gateway has already sent ack or downlink msg", LogLevel.Information);

                        return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.HandledByAnotherGateway);
                    }

                    Logger.Log(loRaDevice.DevEUI, $"down frame counter: {loRaDevice.FCntDown}", LogLevel.Information);
                }

                if (!isConfirmedResubmit)
                {
                    var validFcntUp = isFrameCounterFromNewlyStartedDevice || (payloadFcnt > loRaDevice.FCntUp);
                    if (validFcntUp)
                    {
                        Logger.Log(loRaDevice.DevEUI, $"valid frame counter, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);

                        object payloadData = null;

                        // if it is an upward acknowledgement from the device it does not have a payload
                        // This is confirmation from leaf device that he received a C2D confirmed
                        // if a message payload is null we don't try to decrypt it.
                        if (loraPayload.Frmpayload.Length != 0)
                        {
                            byte[] decryptedPayloadData = null;
                            try
                            {
                                decryptedPayloadData = loraPayload.GetDecryptedPayload(loRaDevice.AppSKey);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(loRaDevice.DevEUI, $"failed to decrypt message: {ex.Message}", LogLevel.Error);
                            }

                            var fportUp = loraPayload.GetFPort();

                            if (string.IsNullOrEmpty(loRaDevice.SensorDecoder))
                            {
                                Logger.Log(loRaDevice.DevEUI, $"no decoder set in device twin. port: {fportUp}", LogLevel.Debug);
                                payloadData = Convert.ToBase64String(decryptedPayloadData);
                            }
                            else
                            {
                                Logger.Log(loRaDevice.DevEUI, $"decoding with: {loRaDevice.SensorDecoder} port: {fportUp}", LogLevel.Debug);
                                payloadData = await this.payloadDecoder.DecodeMessageAsync(decryptedPayloadData, fportUp, loRaDevice.SensorDecoder);
                            }
                        }

                        if (!await this.SendDeviceEventAsync(request, loRaDevice, timeWatcher, payloadData))
                        {
                            // failed to send event to IoT Hub, stop now
                            return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.IoTHubProblem);
                        }

                        loRaDevice.SetFcntUp(payloadFcnt);
                    }
                    else
                    {
                        Logger.Log(loRaDevice.DevEUI, $"invalid frame counter, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);
                    }
                }

                // We check if we have time to futher progress or not
                // C2D checks are quite expensive so if we are really late we just stop here
                var timeToSecondWindow = timeWatcher.GetRemainingTimeToReceiveSecondWindow(loRaDevice);
                if (timeToSecondWindow < LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage)
                {
                    if (requiresConfirmation)
                    {
                        Logger.Log(loRaDevice.DevEUI, $"too late for down message ({timeWatcher.GetElapsedTime()}), sending only ACK to gateway", LogLevel.Information);
                    }

                    return new LoRaDeviceRequestProcessResult(loRaDevice, request);
                }

                // If it is confirmed and
                // - Downlink is disabled for the device or
                // - we don't have time to check c2d and send to device we return now
                if (requiresConfirmation && (!loRaDevice.DownlinkEnabled || timeToSecondWindow.Subtract(LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage) <= LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage))
                {
                    var downlinkMessage = this.CreateDownlinkMessage(
                        null,
                        request,
                        loRaDevice,
                        timeWatcher,
                        false, // fpending
                        (ushort)fcntDown);

                    if (downlinkMessage != null)
                    {
                        _ = request.PacketForwarder.SendDownstreamAsync(downlinkMessage);
                    }

                    return new LoRaDeviceRequestProcessResult(loRaDevice, request, downlinkMessage);
                }

                // Flag indicating if there is another C2D message waiting
                var fpending = false;

                // Contains the Cloud to message we need to send
                Message cloudToDeviceMessage = null;

                if (loRaDevice.DownlinkEnabled)
                {
                    // ReceiveAsync has a longer timeout
                    // But we wait less that the timeout (available time before 2nd window)
                    // if message is received after timeout, keep it in loraDeviceInfo and return the next call
                    var timeAvailableToCheckCloudToDeviceMessages = timeWatcher.GetAvailableTimeToCheckCloudToDeviceMessage(loRaDevice);
                    if (timeAvailableToCheckCloudToDeviceMessages >= LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage)
                    {
                        cloudToDeviceMessage = await loRaDevice.ReceiveCloudToDeviceAsync(timeAvailableToCheckCloudToDeviceMessages);
                        if (cloudToDeviceMessage != null && !this.ValidateCloudToDeviceMessage(loRaDevice, cloudToDeviceMessage))
                        {
                            _ = loRaDevice.CompleteCloudToDeviceMessageAsync(cloudToDeviceMessage);
                            cloudToDeviceMessage = null;
                        }

                        if (cloudToDeviceMessage != null)
                        {
                            if (!requiresConfirmation)
                            {
                                // The message coming from the device was not confirmed, therefore we did not computed the frame count down
                                // Now we need to increment because there is a C2D message to be sent
                                fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, payloadFcnt);

                                if (fcntDown == 0)
                                {
                                    // We did not get a valid frame count down, therefore we should not process the message
                                    _ = loRaDevice.AbandonCloudToDeviceMessageAsync(cloudToDeviceMessage);

                                    cloudToDeviceMessage = null;
                                }
                                else
                                {
                                    requiresConfirmation = true;
                                }

                                Logger.Log(loRaDevice.DevEUI, $"down frame counter: {loRaDevice.FCntDown}", LogLevel.Information);
                            }

                            // Checking again if cloudToDeviceMessage is valid because the fcntDown resolution could have failed,
                            // causing us to drop the message
                            if (cloudToDeviceMessage != null)
                            {
                                var remainingTimeForFPendingCheck = timeWatcher.GetRemainingTimeToReceiveSecondWindow(loRaDevice) - (LoRaOperationTimeWatcher.CheckForCloudMessageCallEstimatedOverhead + LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage);
                                if (remainingTimeForFPendingCheck >= LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage)
                                {
                                    var additionalMsg = await loRaDevice.ReceiveCloudToDeviceAsync(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage);
                                    if (additionalMsg != null)
                                    {
                                        fpending = true;
                                        _ = loRaDevice.AbandonCloudToDeviceMessageAsync(additionalMsg);
                                        Logger.Log(loRaDevice.DevEUI, $"found fpending c2d message id: {additionalMsg.MessageId ?? "undefined"}", LogLevel.Information);
                                    }
                                }
                            }
                        }
                    }
                }

                // No C2D message and request was not confirmed, return nothing
                if (!requiresConfirmation)
                {
                    return new LoRaDeviceRequestProcessResult(loRaDevice, request);
                }

                var confirmDownstream = this.CreateDownlinkMessage(
                    cloudToDeviceMessage,
                    request,
                    loRaDevice,
                    timeWatcher,
                    fpending,
                    (ushort)fcntDown);

                if (cloudToDeviceMessage != null)
                {
                    if (confirmDownstream == null)
                    {
                        Logger.Log(loRaDevice.DevEUI, $"out of time for downstream message, will abandon c2d message id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Information);
                        _ = loRaDevice.AbandonCloudToDeviceMessageAsync(cloudToDeviceMessage);
                    }
                    else
                    {
                        _ = loRaDevice.CompleteCloudToDeviceMessageAsync(cloudToDeviceMessage);
                    }
                }

                if (confirmDownstream != null)
                {
                    _ = request.PacketForwarder.SendDownstreamAsync(confirmDownstream);
                }

                return new LoRaDeviceRequestProcessResult(loRaDevice, request, confirmDownstream);
            }
        }

        private bool ValidateCloudToDeviceMessage(LoRaDevice loRaDevice, Message cloudToDeviceMsg)
        {
            // ensure fport property has been set
            if (!cloudToDeviceMsg.Properties.TryGetValueCaseInsensitive(Constants.FPORT_MSG_PROPERTY_KEY, out var fportValue))
            {
                Logger.Log(loRaDevice.DevEUI, $"missing {Constants.FPORT_MSG_PROPERTY_KEY} property in C2D message '{cloudToDeviceMsg.MessageId}'", LogLevel.Error);
                return false;
            }

            if (byte.TryParse(fportValue, out var fport))
            {
                // ensure fport follows LoRa specification
                // 0    => reserved for mac commands
                // 224+ => reserved for future applications
                if (fport != Constants.LORA_FPORT_RESERVED_MAC_MSG && fport < Constants.LORA_FPORT_RESERVED_FUTURE_START)
                    return true;
            }

            Logger.Log(loRaDevice.DevEUI, $"invalid fport '{fportValue}' in C2D message '{cloudToDeviceMsg.MessageId}'", LogLevel.Error);
            return false;
        }

        /// <summary>
        /// Creates downlink message with ack for confirmation or cloud to device message
        /// </summary>
        private DownlinkPktFwdMessage CreateDownlinkMessage(
            Message cloudToDeviceMessage,
            LoRaRequest request,
            LoRaDevice loRaDevice,
            LoRaOperationTimeWatcher timeWatcher,
            bool fpending,
            ushort fcntDown)
        {
            var upstreamPayload = (LoRaPayloadData)request.Payload;
            var rxpk = request.Rxpk;
            var loraRegion = request.LoRaRegion;

            // default fport
            byte fctrl = 0;
            if (upstreamPayload.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
            {
                // Confirm receiving message to device
                fctrl = (byte)FctrlEnum.Ack;
            }

            byte? fport = null;
            var requiresDeviceAcknowlegement = false;
            byte[] macbytes = null;

            byte[] rndToken = new byte[2];
            Random rnd = new Random();
            rnd.NextBytes(rndToken);

            byte[] frmPayload = null;

            if (cloudToDeviceMessage != null)
            {
                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("cidtype", out var cidTypeValue))
                {
                    Logger.Log(loRaDevice.DevEUI, "Cloud to device MAC command received", LogLevel.Information);
                    MacCommandHolder macCommandHolder = new MacCommandHolder(Convert.ToByte(cidTypeValue));
                    macbytes = macCommandHolder.MacCommand[0].ToBytes();
                }

                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("confirmed", out var confirmedValue) && confirmedValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    requiresDeviceAcknowlegement = true;
                    loRaDevice.LastConfirmedC2DMessageID = cloudToDeviceMessage.MessageId ?? Constants.C2D_MSG_ID_PLACEHOLDER;
                }

                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("fport", out var fPortValue))
                {
                    fport = byte.Parse(fPortValue);
                }

                Logger.Log(loRaDevice.DevEUI, $"Sending a downstream message with ID {ConversionHelper.ByteArrayToString(rndToken)}", LogLevel.Debug);

                frmPayload = cloudToDeviceMessage?.GetBytes();

                Logger.Log(loRaDevice.DevEUI, $"C2D message: {Encoding.UTF8.GetString(frmPayload)}, id: {cloudToDeviceMessage.MessageId ?? "undefined"}, fport: {fport}, confirmed: {requiresDeviceAcknowlegement}, cidType: {cidTypeValue}", LogLevel.Information);

                // cut to the max payload of lora for any EU datarate
                if (frmPayload.Length > 51)
                    Array.Resize(ref frmPayload, 51);

                Array.Reverse(frmPayload);
            }

            if (fpending)
            {
                fctrl |= (int)FctrlEnum.FpendingOrClassB;
            }

            // if (macbytes != null && linkCheckCmdResponse != null)
            //     macbytes = macbytes.Concat(linkCheckCmdResponse).ToArray();
            var srcDevAddr = upstreamPayload.DevAddr.Span;
            var reversedDevAddr = new byte[srcDevAddr.Length];
            for (int i = reversedDevAddr.Length - 1; i >= 0; --i)
            {
                reversedDevAddr[i] = srcDevAddr[srcDevAddr.Length - (1 + i)];
            }

            var msgType = requiresDeviceAcknowlegement ? LoRaMessageType.ConfirmedDataDown : LoRaMessageType.UnconfirmedDataDown;
            var ackLoRaMessage = new LoRaPayloadData(
                msgType,
                reversedDevAddr,
                new byte[] { fctrl },
                BitConverter.GetBytes(fcntDown),
                macbytes,
                fport.HasValue ? new byte[] { fport.Value } : null,
                frmPayload,
                1);

            // var firstWindowTime = timeWatcher.GetRemainingTimeToReceiveFirstWindow(loRaDevice);
            // if (firstWindowTime > TimeSpan.Zero)
            //     System.Threading.Thread.Sleep(firstWindowTime);
            var receiveWindow = timeWatcher.ResolveReceiveWindowToUse(loRaDevice);
            if (receiveWindow == Constants.INVALID_RECEIVE_WINDOW)
                return null;

            string datr;
            double freq;
            long tmst;
            if (receiveWindow == Constants.RECEIVE_WINDOW_2)
            {
                tmst = rxpk.Tmst + timeWatcher.GetReceiveWindow2Delay(loRaDevice) * 1000000;

                if (string.IsNullOrEmpty(this.configuration.Rx2DataRate))
                {
                    Logger.Log(loRaDevice.DevEUI, "using standard second receive windows", LogLevel.Information);
                    freq = loraRegion.RX2DefaultReceiveWindows.frequency;
                    datr = loraRegion.DRtoConfiguration[loraRegion.RX2DefaultReceiveWindows.dr].configuration;
                }

                // if specific twins are set, specify second channel to be as specified
                else
                {
                    freq = this.configuration.Rx2DataFrequency;
                    datr = this.configuration.Rx2DataRate;
                    Logger.Log(loRaDevice.DevEUI, $"using custom DR second receive windows freq : {freq}, datr:{datr}", LogLevel.Information);
                }
            }
            else
            {
                datr = loraRegion.GetDownstreamDR(rxpk);
                freq = loraRegion.GetDownstreamChannelFrequency(rxpk);
                tmst = rxpk.Tmst + timeWatcher.GetReceiveWindow1Delay(loRaDevice) * 1000000;
            }

            // todo: check the device twin preference if using confirmed or unconfirmed down
            return ackLoRaMessage.Serialize(loRaDevice.AppSKey, loRaDevice.NwkSKey, datr, freq, tmst, loRaDevice.DevEUI);
        }

        private async Task<bool> SendDeviceEventAsync(LoRaRequest request, LoRaDevice loRaDevice, LoRaOperationTimeWatcher timeWatcher, object decodedValue)
        {
            var loRaPayloadData = (LoRaPayloadData)request.Payload;
            var deviceTelemetry = new LoRaDeviceTelemetry(request.Rxpk, loRaPayloadData, decodedValue)
            {
                DeviceEUI = loRaDevice.DevEUI,
                GatewayID = this.configuration.GatewayID,
                Edgets = (long)(timeWatcher.Start - DateTime.UnixEpoch).TotalMilliseconds
            };

            Dictionary<string, string> eventProperties = null;
            if (loRaPayloadData.IsUpwardAck())
            {
                eventProperties = new Dictionary<string, string>();
                Logger.Log(loRaDevice.DevEUI, $"Message ack received for C2D message id {loRaDevice.LastConfirmedC2DMessageID}", LogLevel.Information);
                eventProperties.Add(Constants.C2D_MSG_PROPERTY_VALUE_NAME, loRaDevice.LastConfirmedC2DMessageID ?? Constants.C2D_MSG_ID_PLACEHOLDER);
                loRaDevice.LastConfirmedC2DMessageID = null;
            }

            var macCommand = loRaPayloadData.GetMacCommands();
            if (macCommand.MacCommand.Count > 0)
            {
                eventProperties = eventProperties ?? new Dictionary<string, string>();

                for (int i = 0; i < macCommand.MacCommand.Count; i++)
                {
                    eventProperties[macCommand.MacCommand[i].Cid.ToString()] = JsonConvert.SerializeObject(macCommand.MacCommand[i], Formatting.None);

                    // in case it is a link check mac, we need to send it downstream.
                    if (macCommand.MacCommand[i].Cid == CidEnum.LinkCheckCmd)
                    {
                        // linkCheckCmdResponse = new LinkCheckCmd(rxPk.GetModulationMargin(), 1).ToBytes();
                    }
                }
            }

            if (await loRaDevice.SendEventAsync(deviceTelemetry, eventProperties))
            {
                var payloadAsRaw = deviceTelemetry.Data as string;
                if (payloadAsRaw == null && deviceTelemetry.Data != null)
                {
                    payloadAsRaw = JsonConvert.SerializeObject(deviceTelemetry.Data, Formatting.None);
                }

                Logger.Log(loRaDevice.DevEUI, $"message '{payloadAsRaw}' sent to hub", LogLevel.Information);
                return true;
            }

            return false;
        }
    }
}