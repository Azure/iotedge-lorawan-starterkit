// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Mac;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DefaultLoRaDataRequestHandler : ILoRaDataRequestHandler
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private readonly ILoRaPayloadDecoder payloadDecoder;
        private readonly IDeduplicationStrategyFactory deduplicationFactory;
        private readonly ILoRaADRStrategyProvider loRaADRStrategyProvider;
        private readonly ILoRAADRManagerFactory loRaADRManagerFactory;
        private IClassCDeviceMessageSender classCDeviceMessageSender;
        private readonly LoRaWan.NetworkServer.ADR.ILoRAADRManagerFactory loRaADRManagerFactory;
        private readonly IFunctionBundlerProvider functionBundlerProvider;

        public DefaultLoRaDataRequestHandler(
            NetworkServerConfiguration configuration,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            ILoRaPayloadDecoder payloadDecoder,
            IDeduplicationStrategyFactory deduplicationFactory,
            ILoRaADRStrategyProvider loRaADRStrategyProvider,
            ILoRAADRManagerFactory loRaADRManagerFactory,
            IClassCDeviceMessageSender classCDeviceMessageSender = null,
            LoRaWan.NetworkServer.ADR.ILoRAADRManagerFactory loRaADRManagerFactory,
            IFunctionBundlerProvider functionBundlerProvider)
        {
            this.configuration = configuration;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
            this.payloadDecoder = payloadDecoder;
            this.deduplicationFactory = deduplicationFactory;
            this.classCDeviceMessageSender = classCDeviceMessageSender;
            this.loRaADRStrategyProvider = loRaADRStrategyProvider;
            this.loRaADRManagerFactory = loRaADRManagerFactory;
            this.functionBundlerProvider = functionBundlerProvider;
        }

        public async Task<LoRaDeviceRequestProcessResult> ProcessRequestAsync(LoRaRequest request, LoRaDevice loRaDevice)
        {
            var timeWatcher = new LoRaOperationTimeWatcher(request.LoRaRegion, request.StartTime);
            var loraPayload = (LoRaPayloadData)request.Payload;

            var payloadFcnt = loraPayload.GetFcnt();
            var payloadPort = loraPayload.GetFPort();
            var requiresConfirmation = loraPayload.IsConfirmed || loraPayload.IsMacAnswerRequired;

            DeduplicationResult deduplicationResult = null;
            LoRaADRResult loRaADRResult = null;

            var frameCounterStrategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);
            if (frameCounterStrategy == null)
            {
                Logger.Log(loRaDevice.DevEUI, $"failed to resolve frame count update strategy, device gateway: {loRaDevice.GatewayID}, message ignored", LogLevel.Error);
                return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.ApplicationError);
            }

            // Leaf devices that restart lose the counter. In relax mode we accept the incoming frame counter
            // ABP device does not reset the Fcnt so in relax mode we should reset for 0 (LMIC based) or 1
            bool isFrameCounterFromNewlyStartedDevice = DetermineIfFramecounterIsFromNewlyStartedDevice(loRaDevice, payloadFcnt, frameCounterStrategy);

            // Reply attack or confirmed reply
            // Confirmed resubmit: A confirmed message that was received previously but we did not answer in time
            // Device will send it again and we just need to return an ack (but also check for C2D to send it over)
            if (!ValidateRequest(request, isFrameCounterFromNewlyStartedDevice, payloadFcnt, loRaDevice, requiresConfirmation, out bool isConfirmedResubmit, out LoRaDeviceRequestProcessResult result))
            {
                return result;
            }

            var useMultipleGateways = string.IsNullOrEmpty(loRaDevice.GatewayID);
            using (new LoRaDeviceFrameCounterSession(loRaDevice, frameCounterStrategy))
            {
                var bundlerResult = await this.TryUseBundler(request, loRaDevice, loraPayload, useMultipleGateways);

                loRaADRResult = bundlerResult?.AdrResult;

                // ADR should be performed before the deduplication
                // as we still want to collect the signal info, even if we drop
                // it in the next step
                if (loRaADRResult == null && loraPayload.IsAdrEnabled)
                {
                    loRaADRResult = await this.PerformADR(request, loRaDevice, loraPayload, payloadFcnt, loRaADRResult, frameCounterStrategy);
                }

            // Contains the Cloud to message we need to send
            ILoRaCloudToDeviceMessage cloudToDeviceMessage = null;

            using (new LoRaDeviceFrameCounterSession(loRaDevice, frameCounterStrategy))
            {
                // Leaf devices that restart lose the counter. In relax mode we accept the incoming frame counter
                // ABP device does not reset the Fcnt so in relax mode we should reset for 0 (LMIC based) or 1
                var isFrameCounterFromNewlyStartedDevice = false;
                if (payloadFcnt <= 1)
                {
                    // if we got an ADR result or request, we have to send the update to the device
                    requiresConfirmation = true;
                }

                if (useMultipleGateways)
                {
                    // applying the correct deduplication
                    var deduplicationStrategy = this.deduplicationFactory.Create(loRaDevice);
                    if (deduplicationStrategy != null)
                    {
                        deduplicationResult = bundlerResult?.DeduplicationResult
                                              ?? await deduplicationStrategy.ResolveDeduplication(payloadFcnt, loRaDevice.FCntDown, this.configuration.GatewayID);

                        if (!deduplicationResult.CanProcess)
                        {
                            // duplication strategy is indicating that we do not need to continue processing this message
                            Logger.Log(loRaDevice.DevEUI, $"duplication strategy indicated to not process message: ${payloadFcnt}", LogLevel.Information);
                            return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.DeduplicationDrop);
                        }
                    }
                }

                // if the bundler already processed the next framecounter down, use that
                int? fcntDown = loRaADRResult?.FCntDown > 0 ? loRaADRResult.FCntDown : bundlerResult?.NextFCntDown;

                // If it is confirmed it require us to update the frame counter down
                // Multiple gateways: in redis, otherwise in device twin
                if (requiresConfirmation)
                {
                    // If there is a deduplication result should not try to get a fcntDown as it failed
                    if (deduplicationResult == null)
                    {
                        fcntDown = await this.EnsureHasFcntDownAsync(loRaDevice, fcntDown, payloadFcnt, frameCounterStrategy);
                    }

                    // Failed to update the fcnt down
                    // In multi gateway scenarios it means the another gateway was faster than using, can stop now
                    if (!fcntDown.HasValue || fcntDown <= 0)
                    {
                        Logger.Log(loRaDevice.DevEUI, "another gateway has already sent ack or downlink msg", LogLevel.Information);

                        return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.HandledByAnotherGateway);
                    }
                }

                if (!isConfirmedResubmit)
                {
                    var validFcntUp = isFrameCounterFromNewlyStartedDevice || (payloadFcnt > loRaDevice.FCntUp);
                    if (validFcntUp)
                    {
                        Logger.Log(loRaDevice.DevEUI, $"valid frame counter, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);

                        object payloadData = null;
                        byte[] decryptedPayloadData = null;

                        // if it is an upward acknowledgement from the device it does not have a payload
                        // This is confirmation from leaf device that he received a C2D confirmed
                        // if a message payload is null we don't try to decrypt it.
                        if (!loraPayload.IsUpwardAck() || loraPayload.Frmpayload.Length > 0)
                        {
                            if (loraPayload.Frmpayload.Length > 0)
                            {
                                try
                                {
                                    decryptedPayloadData = loraPayload.GetDecryptedPayload(loRaDevice.AppSKey);
                                }
                                catch (Exception ex)
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"failed to decrypt message: {ex.Message}", LogLevel.Error);
                                }
                            }

                            if (payloadPort == Constants.LORA_FPORT_RESERVED_MAC_COMMAND)
                            {
                                if (decryptedPayloadData?.Length > 0)
                                {
                                    loraPayload.MacCommands = MacCommand.CreateMacCommandFromBytes(loRaDevice.DevEUI, decryptedPayloadData);
                                }

                                if (loraPayload.IsMacAnswerRequired)
                                {
                                    fcntDown = await this.EnsureHasFcntDownAsync(loRaDevice, fcntDown, payloadFcnt, frameCounterStrategy);
                                    if (!fcntDown.HasValue || fcntDown <= 0)
                                    {
    return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.HandledByAnotherGateway);
}

requiresConfirmation = true;
}
                            }
                            else
                            {
                                if (string.IsNullOrEmpty(loRaDevice.SensorDecoder))
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"no decoder set in device twin. port: {payloadPort}", LogLevel.Debug);
                                    payloadData = new UndecodedPayload(decryptedPayloadData);
                                }
                                else
                                {
                                    Logger.Log(loRaDevice.DevEUI, $"decoding with: {loRaDevice.SensorDecoder} port: {payloadPort}", LogLevel.Debug);
                                    var decodePayloadResult = await this.payloadDecoder.DecodeMessageAsync(loRaDevice.DevEUI, decryptedPayloadData, payloadPort, loRaDevice.SensorDecoder);
payloadData = decodePayloadResult.GetDecodedPayload();

                                    if (decodePayloadResult.CloudToDeviceMessage != null)
                                    {
                                        if (string.IsNullOrEmpty(decodePayloadResult.CloudToDeviceMessage.DevEUI) || string.Equals(loRaDevice.DevEUI, decodePayloadResult.CloudToDeviceMessage.DevEUI, StringComparison.InvariantCultureIgnoreCase))
                                        {
                                            // sending c2d to same device
                                            cloudToDeviceMessage = decodePayloadResult.CloudToDeviceMessage;
                                            fcntDown = await this.EnsureHasFcntDownAsync(loRaDevice, fcntDown, payloadFcnt, frameCounterStrategy);

                                            if (!fcntDown.HasValue || fcntDown <= 0)
                                            {
    // We did not get a valid frame count down, therefore we should not process the message
    _ = cloudToDeviceMessage.AbandonAsync();

    cloudToDeviceMessage = null;
}
                                            else
                                            {
    requiresConfirmation = true;
}
}
                                        else
                                        {
                                            this.SendClassCDeviceMessage(decodePayloadResult.CloudToDeviceMessage);
                                        }
                                    }
                                }
                            }
                        }

                        // In case it is a Mac Command only we don't want to send it to the IoT Hub
                        if (payloadPort != Constants.LORA_FPORT_RESERVED_MAC_COMMAND)
                        {
                            if (!await this.SendDeviceEventAsync(request, loRaDevice, timeWatcher, payloadData, deduplicationResult, decryptedPayloadData))
                            {
    // failed to send event to IoT Hub, stop now
    return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.IoTHubProblem);
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
                if (timeToSecondWindow<LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage)
                {
                    if (requiresConfirmation)
                    {
                        Logger.Log(loRaDevice.DevEUI, $"too late for down message ({timeWatcher.GetElapsedTime()})", LogLevel.Information);
                    }

                    return new LoRaDeviceRequestProcessResult(loRaDevice, request);
                }

                // If it is confirmed and
                // - Downlink is disabled for the device or
                // - we don't have time to check c2d and send to device we return now
                if (requiresConfirmation && (!loRaDevice.DownlinkEnabled || timeToSecondWindow.Subtract(LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage) <= LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage))
                {
                    var downlinkMessage = DownlinkMessageBuilder.CreateDownlinkMessage(
                        this.configuration,
                        loRaDevice,
                        request,
                        timeWatcher,
                        null,
                        false, // fpending
                        (ushort)fcntDown,
                        loRaADRResult);

                    if (downlinkMessage != null)
                    {
                        _ = request.PacketForwarder.SendDownstreamAsync(downlinkMessage);
                    }

                    return new LoRaDeviceRequestProcessResult(loRaDevice, request, downlinkMessage);
                }

                // Flag indicating if there is another C2D message waiting
                var fpending = false;

                // If downlink is enabled and we did not get a cloud to device message from decoder
                // try to get one from IoT Hub C2D
                if (loRaDevice.DownlinkEnabled && cloudToDeviceMessage == null)
                {
                    // ReceiveAsync has a longer timeout
                    // But we wait less that the timeout (available time before 2nd window)
                    // if message is received after timeout, keep it in loraDeviceInfo and return the next call
                    var timeAvailableToCheckCloudToDeviceMessages = timeWatcher.GetAvailableTimeToCheckCloudToDeviceMessage(loRaDevice);
                    if (timeAvailableToCheckCloudToDeviceMessages >= LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage)
                    {
                        cloudToDeviceMessage = await this.ReceiveCloudToDeviceAsync(loRaDevice, timeAvailableToCheckCloudToDeviceMessages);
                        if (cloudToDeviceMessage != null && !this.ValidateCloudToDeviceMessage(loRaDevice, cloudToDeviceMessage))
                        {
    _ = cloudToDeviceMessage.CompleteAsync();
    cloudToDeviceMessage = null;
}

                        if (cloudToDeviceMessage != null)
                        {
    if (!requiresConfirmation)
    {
        // The message coming from the device was not confirmed, therefore we did not computed the frame count down
        // Now we need to increment because there is a C2D message to be sent
        fcntDown = await this.EnsureHasFcntDownAsync(loRaDevice, fcntDown, payloadFcnt, frameCounterStrategy);

        if (!fcntDown.HasValue || fcntDown <= 0)
        {
            // We did not get a valid frame count down, therefore we should not process the message
            _ = cloudToDeviceMessage.AbandonAsync();

            cloudToDeviceMessage = null;
        }
        else
        {
            requiresConfirmation = true;
        }
    }

    // Checking again if cloudToDeviceMessage is valid because the fcntDown resolution could have failed,
    // causing us to drop the message
    if (cloudToDeviceMessage != null)
    {
        var remainingTimeForFPendingCheck = timeWatcher.GetRemainingTimeToReceiveSecondWindow(loRaDevice) - (LoRaOperationTimeWatcher.CheckForCloudMessageCallEstimatedOverhead + LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage);
        if (remainingTimeForFPendingCheck >= LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage)
        {
            var additionalMsg = await this.ReceiveCloudToDeviceAsync(loRaDevice, LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage);
            if (additionalMsg != null)
            {
                fpending = true;
                Logger.Log(loRaDevice.DevEUI, $"found fpending c2d message id: {additionalMsg.MessageId ?? "undefined"}", LogLevel.Information);
                _ = additionalMsg.AbandonAsync();
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

                var confirmDownstream = DownlinkMessageBuilder.CreateDownlinkMessage(
                    this.configuration,
                    loRaDevice,
                    request,
                    timeWatcher,
                    cloudToDeviceMessage,
                    fpending,
                    (ushort)fcntDown,
                    loRaADRResult);

                if (cloudToDeviceMessage != null)
                {
                    if (confirmDownstream == null)
                    {
                        Logger.Log(loRaDevice.DevEUI, $"out of time for downstream message, will abandon c2d message id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Information);
                        _ = cloudToDeviceMessage.AbandonAsync();
                    }
                    else
                    {
                        _ = cloudToDeviceMessage.CompleteAsync();
                    }
                }

                if (confirmDownstream != null)
                {
                    _ = request.PacketForwarder.SendDownstreamAsync(confirmDownstream);
                }

                return new LoRaDeviceRequestProcessResult(loRaDevice, request, confirmDownstream);
            }
        }

        internal void SetClassCMessageSender(IClassCDeviceMessageSender classCMessageSender) => this.classCDeviceMessageSender = classCMessageSender;
        private static bool ValidateRequest(LoRaRequest request, bool isFrameCounterFromNewlyStartedDevice, ushort payloadFcnt, LoRaDevice loRaDevice, bool requiresConfirmation, out bool isConfirmedResubmit, out LoRaDeviceRequestProcessResult result)
        {
            isConfirmedResubmit = false;
            result = null;
            if (!isFrameCounterFromNewlyStartedDevice && payloadFcnt <= loRaDevice.FCntUp)
            {
                // if it is confirmed most probably we did not ack in time before or device lost the ack packet so we should continue but not send the msg to iothub
                if (requiresConfirmation && payloadFcnt == loRaDevice.FCntUp)
                {
                    if (!loRaDevice.ValidateConfirmResubmit(payloadFcnt))
                    {
                        Logger.Log(loRaDevice.DevEUI, $"resubmit from confirmed message exceeds threshold of {LoRaDevice.MaxConfirmationResubmitCount}, message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Debug);
                        result = new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.ConfirmationResubmitThresholdExceeded);
                        return false;
                    }

                    isConfirmedResubmit = true;
                    Logger.Log(loRaDevice.DevEUI, $"resubmit from confirmed message detected, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);
                }
                else
                {
                    Logger.Log(loRaDevice.DevEUI, $"invalid frame counter, message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);
                    result = new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.InvalidFrameCounter);
                    return false;
                }
            }

            return true;
        }

        private static bool DetermineIfFramecounterIsFromNewlyStartedDevice(LoRaDevice loRaDevice, ushort payloadFcnt, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
        {
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

            return isFrameCounterFromNewlyStartedDevice;
        }

        private async Task<FunctionBundlerResult> TryUseBundler(LoRaRequest request, LoRaDevice loRaDevice, LoRaPayloadData loraPayload, bool useMultipleGateways)
        {
            FunctionBundlerResult bundlerResult = null;
            if (useMultipleGateways)
            {
                var bundler = this.functionBundlerProvider.CreateIfRequired(this.configuration.GatewayID, loraPayload, loRaDevice, this.deduplicationFactory, request);
                if (bundler != null)
                {
                    bundlerResult = await bundler.Execute();
                    if (bundlerResult.NextFCntDown.HasValue)
                    {
                        // we got a new framecounter down. Make sure this
                        // gets saved eventually to the twins
                        loRaDevice.SetFcntDown(bundlerResult.NextFCntDown.Value);
                    }
                }
            }

            return bundlerResult;
        }

        /// <summary>
        /// Perform ADR in case of Single Gateway Scenario
        /// </summary>
        private async Task<LoRaADRResult> PerformADR(LoRaRequest request, LoRaDevice loRaDevice, LoRaPayloadData loraPayload, ushort payloadFcnt, LoRaADRResult loRaADRResult, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
        {
            var loRaADRManager = this.loRaADRManagerFactory.Create(this.loRaADRStrategyProvider, frameCounterStrategy, loRaDevice);

            var loRaADRTableEntry = new LoRaADRTableEntry()
            {
                DevEUI = loRaDevice.DevEUI,
                FCnt = payloadFcnt,
                GatewayId = this.configuration.GatewayID,
                Snr = request.Rxpk.Lsnr
            };

            // If the ADR req bit is not set we don't perform rate adaptation.
            if (!loraPayload.IsAdrReq)
            {
                _ = loRaADRManager.StoreADREntry(loRaADRTableEntry);
            }
            else
            {
                Logger.Log(loRaDevice.DevEUI, $"ADR Ack request received", LogLevel.Information);
                loRaADRResult = await loRaADRManager.CalculateADRResultAndAddEntry(
                    loRaDevice.DevEUI,
                    this.configuration.GatewayID,
                    payloadFcnt,
                    loRaDevice.FCntDown,
                    (float)request.Rxpk.RequiredSnr,
                    request.LoRaRegion.GetDRFromFreqAndChan(request.Rxpk.Datr),
                    request.LoRaRegion.TXPowertoMaxEIRP.Count - 1,
                    loRaADRTableEntry);
            }

            return loRaADRResult;
        }

        private bool ValidateCloudToDeviceMessage(LoRaDevice loRaDevice, Message cloudToDeviceMsg)
        {
            bool containsMacCommand = false;

            // If a C2D message contains a Mac command we don't need to set the fport.
            if (cloudToDeviceMsg.Properties.TryGetValueCaseInsensitive(Constants.C2D_MSG_PROPERTY_MAC_COMMAND, out var macCommand))
            {
                if (!Enum.TryParse(macCommand, out LoRaTools.CidEnum _))
                {
                    Logger.Log(loRaDevice.DevEUI, $"CidEnum type of C2D mac Command {macCommand} could not be parsed", LogLevel.Error);
                    return false;
                }

                containsMacCommand = true;
            }

void SendClassCDeviceMessage(ILoRaCloudToDeviceMessage cloudToDeviceMessage)
{
    if (this.classCDeviceMessageSender != null)
    {
        Task.Run(() => this.classCDeviceMessageSender.SendAsync(cloudToDeviceMessage));
    }
}

private async Task<ILoRaCloudToDeviceMessage> ReceiveCloudToDeviceAsync(LoRaDevice loRaDevice, TimeSpan timeAvailableToCheckCloudToDeviceMessages)
{
    var actualMessage = await loRaDevice.ReceiveCloudToDeviceAsync(timeAvailableToCheckCloudToDeviceMessages);
    return (actualMessage != null) ? new LoRaCloudToDeviceMessageWrapper(loRaDevice, actualMessage) : null;
}

private bool ValidateCloudToDeviceMessage(LoRaDevice loRaDevice, ILoRaCloudToDeviceMessage cloudToDeviceMsg)
{
    if (!cloudToDeviceMsg.IsValid(out var errorMessage))
    {
        Logger.Log(loRaDevice.DevEUI, $"Invalid cloud to device message: {errorMessage}", LogLevel.Error);
        return false;
    }

    // ensure fport follows LoRa specification
    // 0    => reserved for mac commands
    // 224+ => reserved for future applications
    if (cloudToDeviceMsg.Fport >= Constants.LORA_FPORT_RESERVED_FUTURE_START)
    {
        Logger.Log(loRaDevice.DevEUI, $"invalid fport '{cloudToDeviceMsg.Fport}' in C2D message '{cloudToDeviceMsg.MessageId}'", LogLevel.Error);
        return false;
    }

    // fport 0 is reserved for mac commands
    if (cloudToDeviceMsg.Fport == Constants.LORA_FPORT_RESERVED_MAC_COMMAND)
    {
        // Not valid if there is no mac command or there is a payload
        if (cloudToDeviceMsg.MacCommands?.Count == 0 || cloudToDeviceMsg.GetPayload().Length > 0)
        {
            Logger.Log(loRaDevice.DevEUI, $"invalid mac command fport usage in C2D message '{cloudToDeviceMsg.MessageId}'", LogLevel.Error);
            return false;
        }
    }

    return true;
}

private async Task<bool> SendDeviceEventAsync(LoRaRequest request, LoRaDevice loRaDevice, LoRaOperationTimeWatcher timeWatcher, object decodedValue, DeduplicationResult deduplicationResult, byte[] decryptedPayloadData)
{
    var loRaPayloadData = (LoRaPayloadData)request.Payload;
    var deviceTelemetry = new LoRaDeviceTelemetry(request.Rxpk, loRaPayloadData, decodedValue, decryptedPayloadData)
    {
        DeviceEUI = loRaDevice.DevEUI,
        GatewayID = this.configuration.GatewayID,
        Edgets = (long)(timeWatcher.Start - DateTime.UnixEpoch).TotalMilliseconds
    };

    if (deduplicationResult != null && deduplicationResult.IsDuplicate)
    {
        deviceTelemetry.DupMsg = true;
    }

    Dictionary<string, string> eventProperties = null;
    if (loRaPayloadData.IsUpwardAck())
    {
        eventProperties = new Dictionary<string, string>();
        Logger.Log(loRaDevice.DevEUI, $"Message ack received for C2D message id {loRaDevice.LastConfirmedC2DMessageID}", LogLevel.Information);
        eventProperties.Add(Constants.C2D_MSG_PROPERTY_VALUE_NAME, loRaDevice.LastConfirmedC2DMessageID ?? Constants.C2D_MSG_ID_PLACEHOLDER);
        loRaDevice.LastConfirmedC2DMessageID = null;
    }

    this.ProcessAndSendMacCommands(loRaPayloadData, ref eventProperties);

    if (await loRaDevice.SendEventAsync(deviceTelemetry, eventProperties))
    {
        string payloadAsRaw = null;
        if (deviceTelemetry.Data != null)
        {
            payloadAsRaw = JsonConvert.SerializeObject(deviceTelemetry.Data, Formatting.None);
        }

        Logger.Log(loRaDevice.DevEUI, $"message '{payloadAsRaw}' sent to hub", LogLevel.Information);
        return true;
    }

    return false;
}

/// <summary>
/// Send detected MAC commands as message properties.
/// </summary>
void ProcessAndSendMacCommands(LoRaPayloadData payloadData, ref Dictionary<string, string> eventProperties)
{
    if (payloadData.MacCommands?.Count > 0)
    {
        eventProperties = eventProperties ?? new Dictionary<string, string>(payloadData.MacCommands.Count);

        for (int i = 0; i < payloadData.MacCommands.Count; i++)
        {
            eventProperties[payloadData.MacCommands[i].Cid.ToString()] = JsonConvert.SerializeObject(payloadData.MacCommands[i].ToString(), Formatting.None);
        }
    }
}

/// <summary>
/// Helper method to resolve FcntDown in case one was not yet acquired
/// </summary>
/// <returns>0 if the resolution failed or > 0 if a valid frame count was acquired</returns>
async ValueTask<int> EnsureHasFcntDownAsync(
    LoRaDevice loRaDevice,
    int? fcntDown,
    int payloadFcnt,
    ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
{
    if (fcntDown > 0)
        return fcntDown.Value;

    var newFcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, payloadFcnt);

    // Failed to update the fcnt down
    // In multi gateway scenarios it means the another gateway was faster than using, can stop now
    if (newFcntDown <= 0)
    {
        Logger.Log(loRaDevice.DevEUI, "another gateway has already sent ack or downlink msg", LogLevel.Information);
    }
    else
    {
        Logger.Log(loRaDevice.DevEUI, $"down frame counter: {loRaDevice.FCntDown}", LogLevel.Information);
    }

    return newFcntDown;
}
    }
}