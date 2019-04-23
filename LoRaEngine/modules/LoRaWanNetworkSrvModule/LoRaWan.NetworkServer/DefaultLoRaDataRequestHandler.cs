// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.ADR;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer.ADR;
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
        private readonly IFunctionBundlerProvider functionBundlerProvider;
        private IClassCDeviceMessageSender classCDeviceMessageSender;

        public DefaultLoRaDataRequestHandler(
            NetworkServerConfiguration configuration,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            ILoRaPayloadDecoder payloadDecoder,
            IDeduplicationStrategyFactory deduplicationFactory,
            ILoRaADRStrategyProvider loRaADRStrategyProvider,
            ILoRAADRManagerFactory loRaADRManagerFactory,
            IFunctionBundlerProvider functionBundlerProvider,
            IClassCDeviceMessageSender classCDeviceMessageSender = null)
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
            var timeWatcher = request.GetTimeWatcher();
            using (var deviceConnectionActivity = loRaDevice.BeginDeviceClientConnectionActivity())
            {
                if (deviceConnectionActivity == null)
                {
                    return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.DeviceClientConnectionFailed);
                }

                var loraPayload = (LoRaPayloadData)request.Payload;

                var payloadFcnt = loraPayload.GetFcnt();

                uint payloadFcntAdjusted = LoRaPayload.InferUpper32BitsForClientFcnt(payloadFcnt, loRaDevice.FCntUp);
                Logger.Log(loRaDevice.DevEUI, $"converted 16bit FCnt {payloadFcnt} to 32bit FCnt {payloadFcntAdjusted}", LogLevel.Debug);

                var payloadPort = loraPayload.GetFPort();
                var requiresConfirmation = loraPayload.IsConfirmed || loraPayload.IsMacAnswerRequired;

                LoRaADRResult loRaADRResult = null;

                var frameCounterStrategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);
                if (frameCounterStrategy == null)
                {
                    Logger.Log(loRaDevice.DevEUI, $"failed to resolve frame count update strategy, device gateway: {loRaDevice.GatewayID}, message ignored", LogLevel.Error);
                    return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.ApplicationError);
                }

                // Contains the Cloud to message we need to send
                IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage = null;

                // Leaf devices that restart lose the counter. In relax mode we accept the incoming frame counter
                // ABP device does not reset the Fcnt so in relax mode we should reset for 0 (LMIC based) or 1
                bool isFrameCounterFromNewlyStartedDevice = await this.DetermineIfFramecounterIsFromNewlyStartedDeviceAsync(loRaDevice, payloadFcntAdjusted, frameCounterStrategy);

                // Reply attack or confirmed reply
                // Confirmed resubmit: A confirmed message that was received previously but we did not answer in time
                // Device will send it again and we just need to return an ack (but also check for C2D to send it over)
                if (!ValidateRequest(request, isFrameCounterFromNewlyStartedDevice, payloadFcntAdjusted, loRaDevice, requiresConfirmation, out bool isConfirmedResubmit, out LoRaDeviceRequestProcessResult result))
                {
                    return result;
                }

                var useMultipleGateways = string.IsNullOrEmpty(loRaDevice.GatewayID);

                try
                {
                    var bundlerResult = await this.TryUseBundler(request, loRaDevice, loraPayload, useMultipleGateways);

                    loRaADRResult = bundlerResult?.AdrResult;

                    if (bundlerResult?.PreferredGatewayResult != null)
                    {
                        this.HandlePreferredGatewayChanges(request, loRaDevice, bundlerResult);
                    }

                    if (loraPayload.IsAdrReq)
                    {
                        Logger.Log(loRaDevice.DevEUI, $"ADR ack request received", LogLevel.Debug);
                    }

                    // ADR should be performed before the deduplication
                    // as we still want to collect the signal info, even if we drop
                    // it in the next step
                    if (loRaADRResult == null && loraPayload.IsAdrEnabled)
                    {
                        loRaADRResult = await this.PerformADR(request, loRaDevice, loraPayload, payloadFcntAdjusted, loRaADRResult, frameCounterStrategy);
                    }

                    if (loRaADRResult?.CanConfirmToDevice == true || loraPayload.IsAdrReq)
                    {
                        // if we got an ADR result or request, we have to send the update to the device
                        requiresConfirmation = true;
                    }

                    if (useMultipleGateways)
                    {
                        // applying the correct deduplication
                        if (bundlerResult?.DeduplicationResult != null && !bundlerResult.DeduplicationResult.CanProcess)
                        {
                            // duplication strategy is indicating that we do not need to continue processing this message
                            Logger.Log(loRaDevice.DevEUI, $"duplication strategy indicated to not process message: {payloadFcnt}", LogLevel.Debug);
                            return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.DeduplicationDrop);
                        }
                    }
                    else
                    {
                        // we must save class C devices regions in order to send c2d messages
                        if (loRaDevice.ClassType == LoRaDeviceClassType.C && request.Region.LoRaRegion != loRaDevice.LoRaRegion)
                            loRaDevice.UpdateRegion(request.Region.LoRaRegion, acceptChanges: false);
                    }

                    // if deduplication already processed the next framecounter down, use that
                    uint? fcntDown = loRaADRResult?.FCntDown != null ? loRaADRResult.FCntDown : bundlerResult?.NextFCntDown;

                    if (fcntDown.HasValue)
                    {
                        LogFrameCounterDownState(loRaDevice, fcntDown.Value);
                    }

                    // If it is confirmed it require us to update the frame counter down
                    // Multiple gateways: in redis, otherwise in device twin
                    if (requiresConfirmation)
                    {
                        fcntDown = await this.EnsureHasFcntDownAsync(loRaDevice, fcntDown, payloadFcntAdjusted, frameCounterStrategy);

                        // Failed to update the fcnt down
                        // In multi gateway scenarios it means the another gateway was faster than using, can stop now
                        if (fcntDown <= 0)
                        {
                            return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.HandledByAnotherGateway);
                        }
                    }

                    var validFcntUp = isFrameCounterFromNewlyStartedDevice || (payloadFcntAdjusted > loRaDevice.FCntUp);
                    if (validFcntUp || isConfirmedResubmit)
                    {
                        if (!isConfirmedResubmit)
                        {
                            Logger.Log(loRaDevice.DevEUI, $"valid frame counter, msg: {payloadFcntAdjusted} server: {loRaDevice.FCntUp}", LogLevel.Debug);
                        }

                        object payloadData = null;
                        byte[] decryptedPayloadData = null;

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

                        if (payloadPort == LoRaFPort.MacCommand)
                        {
                            if (decryptedPayloadData?.Length > 0)
                            {
                                loraPayload.MacCommands = MacCommand.CreateMacCommandFromBytes(loRaDevice.DevEUI, decryptedPayloadData);
                            }

                            if (loraPayload.IsMacAnswerRequired)
                            {
                                fcntDown = await this.EnsureHasFcntDownAsync(loRaDevice, fcntDown, payloadFcntAdjusted, frameCounterStrategy);
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
                                        fcntDown = await this.EnsureHasFcntDownAsync(loRaDevice, fcntDown, payloadFcntAdjusted, frameCounterStrategy);

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

                        if (!isConfirmedResubmit)
                        {
                            // In case it is a Mac Command only we don't want to send it to the IoT Hub
                            if (payloadPort != LoRaFPort.MacCommand)
                            {
                                if (!await this.SendDeviceEventAsync(request, loRaDevice, timeWatcher, payloadData, bundlerResult?.DeduplicationResult, decryptedPayloadData))
                                {
                                    // failed to send event to IoT Hub, stop now
                                    return new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.IoTHubProblem);
                                }
                            }

                            loRaDevice.SetFcntUp(payloadFcntAdjusted);
                        }
                    }

                    // We check if we have time to futher progress or not
                    // C2D checks are quite expensive so if we are really late we just stop here
                    var timeToSecondWindow = timeWatcher.GetRemainingTimeToReceiveSecondWindow(loRaDevice);
                    if (timeToSecondWindow < LoRaOperationTimeWatcher.ExpectedTimeToPackageAndSendMessage)
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
                        var downlinkMessageBuilderResp = DownlinkMessageBuilder.CreateDownlinkMessage(
                            this.configuration,
                            loRaDevice,
                            request,
                            timeWatcher,
                            cloudToDeviceMessage,
                            false, // fpending
                            fcntDown.GetValueOrDefault(),
                            loRaADRResult);

                        if (downlinkMessageBuilderResp.DownlinkPktFwdMessage != null)
                        {
                            _ = request.PacketForwarder.SendDownstreamAsync(downlinkMessageBuilderResp.DownlinkPktFwdMessage);

                            if (cloudToDeviceMessage != null)
                            {
                                if (downlinkMessageBuilderResp.IsMessageTooLong)
                                {
                                    await cloudToDeviceMessage.AbandonAsync();
                                }
                                else
                                {
                                    await cloudToDeviceMessage.CompleteAsync();
                                }
                            }
                        }

                        return new LoRaDeviceRequestProcessResult(loRaDevice, request, downlinkMessageBuilderResp.DownlinkPktFwdMessage);
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
                            if (cloudToDeviceMessage != null && !this.ValidateCloudToDeviceMessage(loRaDevice, request, cloudToDeviceMessage))
                            {
                                // Reject cloud to device message based on result from ValidateCloudToDeviceMessage
                                _ = cloudToDeviceMessage.RejectAsync();
                                cloudToDeviceMessage = null;
                            }

                            if (cloudToDeviceMessage != null)
                            {
                                if (!requiresConfirmation)
                                {
                                    // The message coming from the device was not confirmed, therefore we did not computed the frame count down
                                    // Now we need to increment because there is a C2D message to be sent
                                    fcntDown = await this.EnsureHasFcntDownAsync(loRaDevice, fcntDown, payloadFcntAdjusted, frameCounterStrategy);

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
                                            Logger.Log(loRaDevice.DevEUI, $"found cloud to device message, setting fpending flag, message id: {additionalMsg.MessageId ?? "undefined"}", LogLevel.Information);
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

                    var confirmDownlinkMessageBuilderResp = DownlinkMessageBuilder.CreateDownlinkMessage(
                        this.configuration,
                        loRaDevice,
                        request,
                        timeWatcher,
                        cloudToDeviceMessage,
                        fpending,
                        fcntDown.GetValueOrDefault(),
                        loRaADRResult);

                    if (cloudToDeviceMessage != null)
                    {
                        if (confirmDownlinkMessageBuilderResp.DownlinkPktFwdMessage == null)
                        {
                            Logger.Log(loRaDevice.DevEUI, $"out of time for downstream message, will abandon cloud to device message id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Information);
                            _ = cloudToDeviceMessage.AbandonAsync();
                        }
                        else if (confirmDownlinkMessageBuilderResp.IsMessageTooLong)
                        {
                            Logger.Log(loRaDevice.DevEUI, $"payload will not fit in current receive window, will abandon cloud to device message id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Error);
                            _ = cloudToDeviceMessage.AbandonAsync();
                        }
                        else
                        {
                            _ = cloudToDeviceMessage.CompleteAsync();
                        }
                    }

                    if (confirmDownlinkMessageBuilderResp.DownlinkPktFwdMessage != null)
                    {
                        _ = request.PacketForwarder.SendDownstreamAsync(confirmDownlinkMessageBuilderResp.DownlinkPktFwdMessage);
                    }

                    return new LoRaDeviceRequestProcessResult(loRaDevice, request, confirmDownlinkMessageBuilderResp.DownlinkPktFwdMessage);
                }
                finally
                {
                    try
                    {
                        await loRaDevice.SaveChangesAsync();
                    }
                    catch (Exception saveChangesException)
                    {
                        Logger.Log(loRaDevice.DevEUI, $"error updating reported properties. {saveChangesException.Message}", LogLevel.Error);
                    }
                }
            }
        }

        void HandlePreferredGatewayChanges(
            LoRaRequest request,
            LoRaDevice loRaDevice,
            FunctionBundlerResult bundlerResult)
        {
            var preferredGatewayResult = bundlerResult.PreferredGatewayResult;
            if (preferredGatewayResult.IsSuccessful())
            {
                var currentIsPreferredGateway = bundlerResult.PreferredGatewayResult.PreferredGatewayID == this.configuration.GatewayID;

                var preferredGatewayChanged = bundlerResult.PreferredGatewayResult.PreferredGatewayID != loRaDevice.PreferredGatewayID;
                if (preferredGatewayChanged)
                    Logger.Log(loRaDevice.DevEUI, $"preferred gateway changed from '{loRaDevice.PreferredGatewayID}' to '{preferredGatewayResult.PreferredGatewayID}'", LogLevel.Debug);

                if (preferredGatewayChanged)
                {
                    loRaDevice.UpdatePreferredGatewayID(bundlerResult.PreferredGatewayResult.PreferredGatewayID, acceptChanges: !currentIsPreferredGateway);
                }

                // Save the region if we are the winning gateway and it changed
                if (request.Region.LoRaRegion != loRaDevice.LoRaRegion)
                {
                    loRaDevice.UpdateRegion(request.Region.LoRaRegion, acceptChanges: !currentIsPreferredGateway);
                }
            }
            else
            {
                Logger.Log(loRaDevice.DevEUI, $"failed to resolve preferred gateway: {preferredGatewayResult}", LogLevel.Error);
            }
        }

        internal void SetClassCMessageSender(IClassCDeviceMessageSender classCMessageSender) => this.classCDeviceMessageSender = classCMessageSender;

        void SendClassCDeviceMessage(IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage)
        {
            if (this.classCDeviceMessageSender != null)
            {
                Task.Run(() => this.classCDeviceMessageSender.SendAsync(cloudToDeviceMessage));
            }
        }

        private async Task<IReceivedLoRaCloudToDeviceMessage> ReceiveCloudToDeviceAsync(LoRaDevice loRaDevice, TimeSpan timeAvailableToCheckCloudToDeviceMessages)
        {
            var actualMessage = await loRaDevice.ReceiveCloudToDeviceAsync(timeAvailableToCheckCloudToDeviceMessages);
            return (actualMessage != null) ? new LoRaCloudToDeviceMessageWrapper(loRaDevice, actualMessage) : null;
        }

        private bool ValidateCloudToDeviceMessage(LoRaDevice loRaDevice, LoRaRequest request, IReceivedLoRaCloudToDeviceMessage cloudToDeviceMsg)
        {
            if (!cloudToDeviceMsg.IsValid(out var errorMessage))
            {
                Logger.Log(loRaDevice.DevEUI, errorMessage, LogLevel.Error);
                return false;
            }

            var rxpk = request.Rxpk;
            var loRaRegion = request.Region;
            uint maxPayload;

            // If preferred Window is RX2, this is the max. payload
            if (loRaDevice.PreferredWindow == Constants.RECEIVE_WINDOW_2)
            {
                // Get max. payload size for RX2, considering possilbe user provided Rx2DataRate
                if (string.IsNullOrEmpty(this.configuration.Rx2DataRate))
                    maxPayload = loRaRegion.DRtoConfiguration[loRaRegion.RX2DefaultReceiveWindows.dr].maxPyldSize;
                else
                    maxPayload = loRaRegion.GetMaxPayloadSize(this.configuration.Rx2DataRate);
            }

            // Otherwise, it is RX1.
            else
            {
                maxPayload = loRaRegion.GetMaxPayloadSize(loRaRegion.GetDownstreamDR(rxpk));
            }

            // Deduct 8 bytes from max payload size.
            maxPayload -= Constants.LORA_PROTOCOL_OVERHEAD_SIZE;

            // Calculate total C2D message size based on optional C2D Mac commands.
            var totalPayload = cloudToDeviceMsg.GetPayload()?.Length ?? 0;

            if (cloudToDeviceMsg.MacCommands?.Count > 0)
            {
                foreach (MacCommand macCommand in cloudToDeviceMsg.MacCommands)
                {
                    totalPayload += macCommand.Length;
                }
            }

            // C2D message and optional C2D Mac commands are bigger than max. payload size: REJECT.
            // This message can never be delivered.
            if (totalPayload > maxPayload)
            {
                Logger.Log(loRaDevice.DevEUI, $"message payload size ({totalPayload}) exceeds maximum allowed payload size ({maxPayload}) in cloud to device message", LogLevel.Error);
                return false;
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
                Logger.Log(loRaDevice.DevEUI, $"message ack received for cloud to device message id {loRaDevice.LastConfirmedC2DMessageID}", LogLevel.Information);
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
        async ValueTask<uint> EnsureHasFcntDownAsync(
            LoRaDevice loRaDevice,
            uint? fcntDown,
            uint payloadFcnt,
            ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
        {
            if (fcntDown.HasValue)
            {
                return fcntDown.Value;
            }

            var newFcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, payloadFcnt);

            // Failed to update the fcnt down
            // In multi gateway scenarios it means the another gateway was faster than using, can stop now
            LogFrameCounterDownState(loRaDevice, newFcntDown);

            return newFcntDown;
        }

        private static void LogFrameCounterDownState(LoRaDevice loRaDevice, uint newFcntDown)
        {
            if (newFcntDown <= 0)
            {
                Logger.Log(loRaDevice.DevEUI, "another gateway has already sent ack or downlink msg", LogLevel.Debug);
            }
            else
            {
                Logger.Log(loRaDevice.DevEUI, $"down frame counter: {loRaDevice.FCntDown}", LogLevel.Debug);
            }
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

        private async Task<LoRaADRResult> PerformADR(LoRaRequest request, LoRaDevice loRaDevice, LoRaPayloadData loraPayload, uint payloadFcnt, LoRaADRResult loRaADRResult, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
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
                _ = loRaADRManager.StoreADREntryAsync(loRaADRTableEntry);
            }
            else
            {
                loRaADRResult = await loRaADRManager.CalculateADRResultAndAddEntryAsync(
                    loRaDevice.DevEUI,
                    this.configuration.GatewayID,
                    payloadFcnt,
                    loRaDevice.FCntDown,
                    (float)request.Rxpk.RequiredSnr,
                    request.Region.GetDRFromFreqAndChan(request.Rxpk.Datr),
                    request.Region.TXPowertoMaxEIRP.Count - 1,
                    request.Region.MaxADRDataRate,
                    loRaADRTableEntry);
                Logger.Log(loRaDevice.DevEUI, $"device sent ADR ack request, computing an answer", LogLevel.Debug);
            }

            return loRaADRResult;
        }

        private static bool ValidateRequest(LoRaRequest request, bool isFrameCounterFromNewlyStartedDevice, uint payloadFcnt, LoRaDevice loRaDevice, bool requiresConfirmation, out bool isConfirmedResubmit, out LoRaDeviceRequestProcessResult result)
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
                        Logger.Log(loRaDevice.DevEUI, $"resubmit from confirmed message exceeds threshold of {LoRaDevice.MaxConfirmationResubmitCount}, message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Error);
                        result = new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.ConfirmationResubmitThresholdExceeded);
                        return false;
                    }

                    isConfirmedResubmit = true;
                    Logger.Log(loRaDevice.DevEUI, $"resubmit from confirmed message detected, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Information);
                }
                else
                {
                    Logger.Log(loRaDevice.DevEUI, $"invalid frame counter, message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Debug);
                    result = new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.InvalidFrameCounter);
                    return false;
                }
            }

            // ensuring the framecount difference between the node and the server
            // is <= MAX_FCNT_GAP
            var diff = payloadFcnt > loRaDevice.FCntUp ? payloadFcnt - loRaDevice.FCntUp : loRaDevice.FCntUp - payloadFcnt;
            var valid = diff <= Constants.MAX_FCNT_GAP;

            if (!valid)
            {
                Logger.Log(loRaDevice.DevEUI, $"invalid frame counter (diverges too much), message ignored, msg: {payloadFcnt} server: {loRaDevice.FCntUp}", LogLevel.Error);
                result = new LoRaDeviceRequestProcessResult(loRaDevice, request, LoRaDeviceRequestFailedReason.InvalidFrameCounter);
            }

            return valid;
        }

        private async Task<bool> DetermineIfFramecounterIsFromNewlyStartedDeviceAsync(LoRaDevice loRaDevice, uint payloadFcnt, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
        {
            var isFrameCounterFromNewlyStartedDevice = false;
            if (payloadFcnt <= 1)
            {
                if (loRaDevice.IsABP)
                {
                    if (loRaDevice.IsABPRelaxedFrameCounter && loRaDevice.FCntUp >= 0 && payloadFcnt <= 1)
                    {
                        // known problem when device restarts, starts fcnt from zero
                        // We need to await this reset to avoid races on the server with deduplication and
                        // fcnt down calculations
                        await frameCounterStrategy.ResetAsync(loRaDevice, payloadFcnt, this.configuration.GatewayID);
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
    }
}
