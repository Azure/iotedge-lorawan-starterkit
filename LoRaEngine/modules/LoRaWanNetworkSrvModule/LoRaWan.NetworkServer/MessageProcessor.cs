﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Mac;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Message processor
    /// </summary>
    [Obsolete("replaced by MessageDispatcher", true)]
    public class MessageProcessor
    {
        // Defines Cloud to device message property containing fport value
        internal const string FPORT_MSG_PROPERTY_KEY = "fport";

        // Fport value reserved for mac commands
        const byte LORA_FPORT_RESERVED_MAC_MSG = 0;

        // Starting Fport value reserved for future applications
        const byte LORA_FPORT_RESERVED_FUTURE_START = 224;

        // Default value of a C2D message id if missing from the message
        internal const string C2D_MSG_ID_PLACEHOLDER = "ConfirmationC2DMessageWithNoId";

        // Name of the upstream message property reporint a confirmed message
        internal const string C2D_MSG_PROPERTY_VALUE_NAME = "C2DMsgConfirmed";

        // Name of the mac command message property
        internal const string C2D_MSG_PROPERTY_MAC_COMMAND = "CidType";

        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private readonly ILoRaPayloadDecoder payloadDecoder;
        private volatile Region loraRegion;

        public MessageProcessor(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry deviceRegistry,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            ILoRaPayloadDecoder payloadDecoder)
        {
            this.configuration = configuration;
            this.deviceRegistry = deviceRegistry;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
            this.payloadDecoder = payloadDecoder;

            // Register frame counter initializer
            // It will take care of seeding ABP devices created here for single gateway scenarios
            this.deviceRegistry.RegisterDeviceInitializer(new FrameCounterLoRaDeviceInitializer(configuration.GatewayID, frameCounterUpdateStrategyProvider));
        }

        /// <summary>
        /// Process a raw message
        /// </summary>
        /// <param name="rxpk"><see cref="Rxpk"/> representing incoming message</param>
        /// <returns>A <see cref="DownlinkPktFwdMessage"/> if a message has to be sent back to device</returns>
        public Task<DownlinkPktFwdMessage> ProcessMessageAsync(Rxpk rxpk) => this.ProcessMessageAsync(rxpk, DateTime.UtcNow);

        /// <summary>
        /// Process a raw message
        /// </summary>
        /// <param name="rxpk"><see cref="Rxpk"/> representing incoming message</param>
        /// <param name="startTimeProcessing">Starting time counting from the moment the message was received</param>
        /// <returns>A <see cref="DownlinkPktFwdMessage"/> if a message has to be sent back to device</returns>
        public async Task<DownlinkPktFwdMessage> ProcessMessageAsync(Rxpk rxpk, DateTime startTimeProcessing)
        {
            if (!LoRaPayload.TryCreateLoRaPayload(rxpk, out LoRaPayload loRaPayload))
            {
                Logger.Log("There was a problem in decoding the Rxpk", LogLevel.Error);
                return null;
            }

            if (this.loraRegion == null)
            {
                if (!RegionFactory.TryResolveRegion(rxpk))
                {
                    // log is generated in Region factory
                    // move here once V2 goes GA
                    return null;
                }

                this.loraRegion = RegionFactory.CurrentRegion;
            }

            if (loRaPayload.LoRaMessageType == LoRaMessageType.JoinRequest)
            {
                return await this.ProcessJoinRequestAsync(rxpk, (LoRaPayloadJoinRequest)loRaPayload, startTimeProcessing);
            }
            else if (loRaPayload.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp || loRaPayload.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
            {
                return await this.ProcessDataMessageAsync(rxpk, (LoRaPayloadData)loRaPayload, startTimeProcessing);
            }

            Logger.Log("Unknwon message type in rxpk, message ignored", LogLevel.Error);
            return null;
        }

        /// <summary>
        /// Process LoRa message where the payload is of type LoRaPayloadData
        /// </summary>
        async Task<DownlinkPktFwdMessage> ProcessDataMessageAsync(LoRaTools.LoRaPhysical.Rxpk rxpk, LoRaPayloadData loraPayload, DateTime startTime)
        {
            var devAddr = loraPayload.DevAddr;

            var timeWatcher = new LoRaOperationTimeWatcher(this.loraRegion, startTime);
            using (var processLogger = new ProcessLogger(timeWatcher, devAddr))
            {
                if (!this.IsValidNetId(loraPayload.GetDevAddrNetID(), this.configuration.NetId))
                {
                    Logger.Log(ConversionHelper.ByteArrayToString(devAddr), "device is using another network id, ignoring this message", LogLevel.Debug);
                    processLogger.LogLevel = LogLevel.Debug;
                    return null;
                }

                // Find device that matches:
                // - devAddr
                // - mic check (requires: loraDeviceInfo.NwkSKey or loraDeviceInfo.AppKey, rxpk.LoraPayload.Mic)
                // - gateway id
                var loRaDevice = await this.deviceRegistry.GetDeviceForPayloadAsync(loraPayload);
                if (loRaDevice == null)
                {
                    Logger.Log(ConversionHelper.ByteArrayToString(devAddr), $"device is not from our network, ignoring message", LogLevel.Information);
                    return null;
                }

                // Add context to logger
                processLogger.SetDevEUI(loRaDevice.DevEUI);

                var frameCounterStrategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);

                var payloadFcnt = loraPayload.GetFcnt();
                var requiresConfirmation = loraPayload.IsConfirmed() || loraPayload.IsMacAnswerRequired();

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
                                processLogger.LogLevel = LogLevel.Debug;
                                return null;
                            }

                            isConfirmedResubmit = true;
                            Logger.Log(loRaDevice.DevEUI, $"resubmit from confirmed message detected, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);
                        }
                        else
                        {
                            Logger.Log(loRaDevice.DevEUI, $"invalid frame counter, message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);
                            return null;
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

                            return null;
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
                                    // In case of a Mac command only payload
                                    if (loraPayload.GetFPort() == LORA_FPORT_RESERVED_MAC_MSG)
                                    {
                                        loraPayload.MacCommands = MacCommand.CreateMacCommandFromBytes(loRaDevice.DevEUI, loraPayload.GetDecryptedPayload(loRaDevice.NwkSKey));
                                        requiresConfirmation = loraPayload.IsConfirmed() || loraPayload.IsMacAnswerRequired();
                                    }
                                    else
                                    {
                                        decryptedPayloadData = loraPayload.GetDecryptedPayload(loRaDevice.AppSKey);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"failed to decrypt message: {ex.Message}", LogLevel.Error);
                                }

                                var fportUp = loraPayload.GetFPort();

                                // If contains a command only payload
                                if (decryptedPayloadData != null)
                                {
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
                            }

                            if (loraPayload.GetFPort() != LORA_FPORT_RESERVED_MAC_MSG)
                            {
                                if (!await this.SendDeviceEventAsync(loRaDevice, rxpk, payloadData, loraPayload, timeWatcher))
                                {
                                    // failed to send event to IoT Hub, stop now
                                    return null;
                                }
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

                        return null;
                    }

                    // If it is confirmed and
                    // - Downlink is disabled for the device or
                    // - we don't have time to check c2d and send to device we return now
                    if (requiresConfirmation && (!loRaDevice.DownlinkEnabled || timeToSecondWindow.Subtract(LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage) <= LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage))
                    {
                        return this.CreateDownlinkMessage(
                            null,
                            loRaDevice,
                            rxpk,
                            loraPayload,
                            timeWatcher,
                            devAddr,
                            false, // fpending
                            (ushort)fcntDown);
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
                        return null;
                    }

                    var confirmDownstream = this.CreateDownlinkMessage(
                        cloudToDeviceMessage,
                        loRaDevice,
                        rxpk,
                        loraPayload,
                        timeWatcher,
                        devAddr,
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

                    return confirmDownstream;
                }
            }
        }

        /// <summary>
        /// Creates downlink message with ack for confirmation or cloud to device message
        /// </summary>
        DownlinkPktFwdMessage CreateDownlinkMessage(
            Message cloudToDeviceMessage,
            LoRaDevice loRaDevice,
            Rxpk rxpk,
            LoRaPayloadData upstreamPayload,
            LoRaOperationTimeWatcher timeWatcher,
            ReadOnlyMemory<byte> payloadDevAddr,
            bool fpending,
            ushort fcntDown)
        {
            // default fport
            byte fctrl = 0;
            if (upstreamPayload.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
            {
                // Confirm receiving message to device
                fctrl = (byte)FctrlEnum.Ack;
            }

            ICollection<MacCommand> macCommands = this.PrepareMacCommandAnswer(loRaDevice.DevEUI,  upstreamPayload, cloudToDeviceMessage, rxpk);

            byte? fport = null;
            var requiresDeviceAcknowlegement = false;

            byte[] rndToken = new byte[2];
            Random rnd = new Random();
            rnd.NextBytes(rndToken);

            byte[] frmPayload = null;

            if (cloudToDeviceMessage != null)
            {
                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("confirmed", out var confirmedValue) && confirmedValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    requiresDeviceAcknowlegement = true;
                    loRaDevice.LastConfirmedC2DMessageID = cloudToDeviceMessage.MessageId ?? C2D_MSG_ID_PLACEHOLDER;
                }

                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("fport", out var fPortValue))
                {
                    fport = byte.Parse(fPortValue);
                }

                Logger.Log(loRaDevice.DevEUI, $"Sending a downstream message with ID {ConversionHelper.ByteArrayToString(rndToken)}", LogLevel.Debug);

                frmPayload = cloudToDeviceMessage?.GetBytes();

                Logger.Log(loRaDevice.DevEUI, $"C2D message: {Encoding.UTF8.GetString(frmPayload)}, id: {cloudToDeviceMessage.MessageId ?? "undefined"}, fport: {fport ?? 0}, confirmed: {requiresDeviceAcknowlegement}, macCommand: {(macCommands.Count > 0 ? true : false)}", LogLevel.Information);

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
            var reversedDevAddr = new byte[payloadDevAddr.Length];
            var srcDevAddr = payloadDevAddr.Span;
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
                macCommands,
                fport.HasValue ? new byte[] { fport.Value } : null,
                frmPayload,
                1);

            // var firstWindowTime = timeWatcher.GetRemainingTimeToReceiveFirstWindow(loRaDevice);
            // if (firstWindowTime > TimeSpan.Zero)
            //     System.Threading.Thread.Sleep(firstWindowTime);
            var receiveWindow = timeWatcher.ResolveReceiveWindowToUse(loRaDevice);
            if (receiveWindow == Constants.INVALID_RECEIVE_WINDOW)
                return null;

            string datr = null;
            double freq = 0;
            long tmst = 0;
            if (receiveWindow == Constants.RECEIVE_WINDOW_2)
            {
                tmst = rxpk.Tmst + timeWatcher.GetReceiveWindow2Delay(loRaDevice) * 1000000;

                if (string.IsNullOrEmpty(this.configuration.Rx2DataRate))
                {
                    Logger.Log(loRaDevice.DevEUI, "using standard second receive windows", LogLevel.Information);
                    freq = this.loraRegion.RX2DefaultReceiveWindows.frequency;
                    datr = this.loraRegion.DRtoConfiguration[this.loraRegion.RX2DefaultReceiveWindows.dr].configuration;
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
                try
                {
                    datr = this.loraRegion.GetDownstreamDR(rxpk);
                    freq = this.loraRegion.GetDownstreamChannelFrequency(rxpk);
                    tmst = rxpk.Tmst + this.loraRegion.Receive_delay1 * 1000000;
                }
                catch (RegionLimitException ex)
                {
                    Logger.Log(loRaDevice.DevEUI, ex.Message, LogLevel.Error);
                    return null;
                }
            }

            // todo: check the device twin preference if using confirmed or unconfirmed down
            return ackLoRaMessage.Serialize(loRaDevice.AppSKey, loRaDevice.NwkSKey, datr, freq, tmst, loRaDevice.DevEUI);
        }

        private bool ValidateCloudToDeviceMessage(LoRaDevice loRaDevice, Message cloudToDeviceMsg)
        {
            bool containMacCommand = false;
            // If a C2D message contains a Mac command we don't need to set the fport.
            if (cloudToDeviceMsg.Properties.TryGetValueCaseInsensitive(C2D_MSG_PROPERTY_MAC_COMMAND, out var macCommand))
            {
                if (!Enum.TryParse(macCommand, out CidEnum cid))
                {
                    Logger.Log(loRaDevice.DevEUI, $"CidEnum type of C2D mac Command {macCommand} could not be parsed", LogLevel.Error);
                }
                else
                {
                    containMacCommand = true;
                }
            }

            if (cloudToDeviceMsg.GetBytes().Count() > 0)
            {
                // ensure fport property has been set
                if (!cloudToDeviceMsg.Properties.TryGetValueCaseInsensitive(FPORT_MSG_PROPERTY_KEY, out var fportValue))
                {
                    Logger.Log(loRaDevice.DevEUI, $"missing {FPORT_MSG_PROPERTY_KEY} property in C2D message '{cloudToDeviceMsg.MessageId}'", LogLevel.Error);
                    return false;
                }

                if (byte.TryParse(fportValue, out var fport))
                {
                    // ensure fport follows LoRa specification
                    // 0    => reserved for mac commands
                    // 224+ => reserved for future applications
                    if (fport != LORA_FPORT_RESERVED_MAC_MSG && fport < LORA_FPORT_RESERVED_FUTURE_START)
                        return true;
                }

                Logger.Log(loRaDevice.DevEUI, $"invalid fport '{fportValue}' in C2D message '{cloudToDeviceMsg.MessageId}'", LogLevel.Error);
            }

            return containMacCommand;
        }

        // Sends device telemetry data to IoT Hub
        private async Task<bool> SendDeviceEventAsync(LoRaDevice loRaDevice, Rxpk rxpk, object decodedValue, LoRaPayloadData loRaPayloadData, LoRaOperationTimeWatcher timeWatcher)
        {
            var deviceTelemetry = new LoRaDeviceTelemetry(rxpk, loRaPayloadData, decodedValue)
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
                eventProperties.Add(C2D_MSG_PROPERTY_VALUE_NAME, loRaDevice.LastConfirmedC2DMessageID ?? C2D_MSG_ID_PLACEHOLDER);
                loRaDevice.LastConfirmedC2DMessageID = null;
            }

            this.ProcessAndSendMacCommands(loRaPayloadData, ref eventProperties);

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

        bool IsValidNetId(byte devAddrNwkid, uint netId)
        {
            var netIdBytes = BitConverter.GetBytes(netId);
            devAddrNwkid = (byte)(devAddrNwkid >> 1);
            return devAddrNwkid == (netIdBytes[0] & 0b01111111);
        }

        /// <summary>
        /// Process OTAA join request
        /// </summary>
        async Task<DownlinkPktFwdMessage> ProcessJoinRequestAsync(Rxpk rxpk, LoRaPayloadJoinRequest joinReq, DateTime startTimeProcessing)
        {
            var timeWatcher = new LoRaOperationTimeWatcher(this.loraRegion, startTimeProcessing);
            using (var processLogger = new ProcessLogger(timeWatcher))
            {
                byte[] udpMsgForPktForwarder = new byte[0];

                var devEUI = joinReq.GetDevEUIAsString();
                var appEUI = joinReq.GetAppEUIAsString();

                // set context to logger
                processLogger.SetDevEUI(devEUI);

                var devNonce = joinReq.GetDevNonceAsString();
                Logger.Log(devEUI, $"join request received", LogLevel.Information);

                var loRaDevice = await this.deviceRegistry.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce);
                if (loRaDevice == null)
                    return null;

                if (string.IsNullOrEmpty(loRaDevice.AppKey))
                {
                    Logger.Log(loRaDevice.DevEUI, "join refused: missing AppKey for OTAA device", LogLevel.Error);
                    return null;
                }

                if (loRaDevice.AppEUI != appEUI)
                {
                    Logger.Log(devEUI, "join refused: AppEUI for OTAA does not match device", LogLevel.Error);
                    return null;
                }

                if (!joinReq.CheckMic(loRaDevice.AppKey))
                {
                    Logger.Log(devEUI, "join refused: invalid MIC", LogLevel.Error);
                    return null;
                }

                // Make sure that is a new request and not a replay
                if (!string.IsNullOrEmpty(loRaDevice.DevNonce) && loRaDevice.DevNonce == devNonce)
                {
                    Logger.Log(devEUI, "join refused: DevNonce already used by this device", LogLevel.Information);
                    loRaDevice.IsOurDevice = false;
                    return null;
                }

                // Check that the device is joining through the linked gateway and not another
                if (!string.IsNullOrEmpty(loRaDevice.GatewayID) && !string.Equals(loRaDevice.GatewayID, this.configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase))
                {
                    Logger.Log(devEUI, $"join refused: trying to join not through its linked gateway, ignoring join request", LogLevel.Information);
                    loRaDevice.IsOurDevice = false;
                    return null;
                }

                var netIdBytes = BitConverter.GetBytes(this.configuration.NetId);
                var netId = new byte[3]
                {
                    netIdBytes[0],
                    netIdBytes[1],
                    netIdBytes[2]
                };
                var appNonce = OTAAKeysGenerator.GetAppNonce();
                var appNonceBytes = LoRaTools.Utils.ConversionHelper.StringToByteArray(appNonce);
                var appKeyBytes = LoRaTools.Utils.ConversionHelper.StringToByteArray(loRaDevice.AppKey);
                var appSKey = OTAAKeysGenerator.CalculateKey(new byte[1] { 0x02 }, appNonceBytes, netId, joinReq.DevNonce, appKeyBytes);
                var nwkSKey = OTAAKeysGenerator.CalculateKey(new byte[1] { 0x01 }, appNonceBytes, netId, joinReq.DevNonce, appKeyBytes);
                var devAddr = OTAAKeysGenerator.GetNwkId(netId);

                if (!timeWatcher.InTimeForJoinAccept())
                {
                    // in this case it's too late, we need to break and avoid saving twins
                    Logger.Log(devEUI, $"join refused: processing of the join request took too long, sending no message", LogLevel.Information);
                    return null;
                }

                Logger.Log(loRaDevice.DevEUI, $"saving join properties twins", LogLevel.Debug);
                var deviceUpdateSucceeded = await loRaDevice.UpdateAfterJoinAsync(devAddr, nwkSKey, appSKey, appNonce, devNonce, LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId));
                Logger.Log(loRaDevice.DevEUI, $"done saving join properties twins", LogLevel.Debug);

                if (!deviceUpdateSucceeded)
                {
                    Logger.Log(devEUI, $"join refused: join request could not save twins", LogLevel.Error);
                    return null;
                }

                var windowToUse = timeWatcher.ResolveJoinAcceptWindowToUse(loRaDevice);
                if (windowToUse == 0)
                {
                    Logger.Log(devEUI, $"join refused: processing of the join request took too long, sending no message", LogLevel.Information);
                    return null;
                }

                double freq = 0;
                string datr = null;
                uint tmst = 0;
                if (windowToUse == Constants.RECEIVE_WINDOW_1)
                {
                    try
                    {
                        datr = this.loraRegion.GetDownstreamDR(rxpk);
                        freq = this.loraRegion.GetDownstreamChannelFrequency(rxpk);
                    }
                    catch (RegionLimitException ex)
                    {
                        Logger.Log(devEUI, ex.ToString(), LogLevel.Error);
                    }

                    // set tmst for the normal case
                    tmst = rxpk.Tmst + this.loraRegion.Join_accept_delay1 * 1000000;
                }
                else
                {
                    Logger.Log(devEUI, $"processing of the join request took too long, using second join accept receive window", LogLevel.Information);
                    tmst = rxpk.Tmst + this.loraRegion.Join_accept_delay2 * 1000000;
                    if (string.IsNullOrEmpty(this.configuration.Rx2DataRate))
                    {
                        Logger.Log(devEUI, $"using standard second receive windows for join request", LogLevel.Information);
                        // using EU fix DR for RX2
                        freq = this.loraRegion.RX2DefaultReceiveWindows.frequency;
                        datr = this.loraRegion.DRtoConfiguration[RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.dr].configuration;
                    }
                    else
                    {
                        Logger.Log(devEUI, $"using custom  second receive windows for join request", LogLevel.Information);
                        freq = this.configuration.Rx2DataFrequency;
                        datr = this.configuration.Rx2DataRate;
                    }
                }

                loRaDevice.IsOurDevice = true;
                this.deviceRegistry.UpdateDeviceAfterJoin(loRaDevice);

                // Build join accept downlink message
                Array.Reverse(netId);
                Array.Reverse(appNonceBytes);

                return this.CreateJoinAcceptDownlinkMessage(
                    netId,
                    loRaDevice.AppKey,
                    devAddr,
                    appNonceBytes,
                    datr,
                    freq,
                    tmst,
                    devEUI);
            }
        }

        /// <summary>
        /// Creates downlink message for join accept
        /// </summary>
        DownlinkPktFwdMessage CreateJoinAcceptDownlinkMessage(
            ReadOnlyMemory<byte> netId,
            string appKey,
            string devAddr,
            ReadOnlyMemory<byte> appNonce,
            string datr,
            double freq,
            long tmst,
            string devEUI)
        {
            var loRaPayloadJoinAccept = new LoRaTools.LoRaMessage.LoRaPayloadJoinAccept(
                LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId), // NETID 0 / 1 is default test
                ConversionHelper.StringToByteArray(devAddr), // todo add device address management
                appNonce.ToArray(),
                new byte[] { 0 },
                new byte[] { 0 },
                null);

            return loRaPayloadJoinAccept.Serialize(appKey, datr, freq, tmst, devEUI);
        }

        /// <summary>
        /// Send detected MAC commands as message properties.
        /// </summary>
        public void ProcessAndSendMacCommands(LoRaPayloadData payloadData, ref Dictionary<string, string> eventProperties)
        {
            var macCommands = payloadData.GetMacCommands();
            if (macCommands?.Count > 0)
            {
                eventProperties = eventProperties ?? new Dictionary<string, string>();

                for (int i = 0; i < macCommands.Count; i++)
                {
                    eventProperties[macCommands[i].Cid.ToString()] = JsonConvert.SerializeObject(macCommands[i].ToString(), Formatting.None);
                }
            }
        }

        /// <summary>
        /// Prepare the Mac Commands to be sent in the downstream message.
        /// </summary>
        public ICollection<MacCommand> PrepareMacCommandAnswer(string devEUI, LoRaPayloadData loRaPayload, Message cloudToDeviceMessage, Rxpk rxpk)
        {
            Dictionary<int, MacCommand> macCommands = new Dictionary<int, MacCommand>();

            // Check if the device sent a Mac Command requiring a response. Currently only LinkCheck requires an answer.
            if (loRaPayload.IsMacAnswerRequired())
            {
                // Todo, check how I could see how many gateway received the message
                var linkCheckAnswer = new LinkCheckAnswer(rxpk.GetModulationMargin(), 1);
                macCommands.Add(
                    (int)CidEnum.LinkCheckCmd,
                    linkCheckAnswer);
            }

            if (cloudToDeviceMessage != null)
            {
                // Check for Mac cloud to devices requests.
                if (cloudToDeviceMessage.Properties.TryGetValueCaseInsensitive("cidtype", out var cidTypeValue))
                {
                    try
                    {
                        var macCmd = MacCommand.CreateMacCommandFromC2DMessage(cidTypeValue, cloudToDeviceMessage.Properties);
                        if (!macCommands.TryAdd((int)macCmd.Cid, macCmd))
                        {
                            Logger.Log(devEUI, $"Could not send the C2D Mac Command {cidTypeValue}, as such a property was already present in the message. Please resend the C2D", LogLevel.Error);
                        }

                        Logger.Log(devEUI, $"Cloud to device MAC command {cidTypeValue} received {macCmd}", LogLevel.Information);
                    }
                    catch (MacCommandException ex)
                    {
                        Logger.Log(devEUI, ex.ToString(), LogLevel.Error);
                   }
                }
            }

            // TODO Implement ADR control Logic
            return macCommands.Values;
        }
    }
}