// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using LoRaTools;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Mac;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Helper class to create <see cref="DownlinkMessage"/>.
    /// </summary>
    internal static class DownlinkMessageBuilder
    {
        private static readonly RandomNumberGenerator RndKeysGenerator = RandomNumberGenerator.Create();

        /// <summary>
        /// Creates downlink message with ack for confirmation or cloud to device message.
        /// </summary>
        internal static DownlinkMessageBuilderResponse CreateDownlinkMessage(
            NetworkServerConfiguration configuration,
            LoRaDevice loRaDevice,
            LoRaRequest request,
            LoRaOperationTimeWatcher timeWatcher,
            IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage,
            bool fpending,
            uint fcntDown,
            LoRaADRResult loRaADRResult,
            ILogger logger)
        {
            var fcntDownToSend = ValidateAndConvert16bitFCnt(fcntDown);

            var upstreamPayload = (LoRaPayloadData)request.Payload;
            var radioMetadata = request.RadioMetadata;
            var loRaRegion = request.Region;
            var isMessageTooLong = false;

            // default fport
            var fctrl = FrameControlFlags.None;
            if (upstreamPayload.MessageType == MacMessageType.ConfirmedDataUp)
            {
                // Confirm receiving message to device
                fctrl = FrameControlFlags.Ack;
            }

            // Calculate receive window
            var receiveWindow = timeWatcher.ResolveReceiveWindowToUse(loRaDevice);
            if (receiveWindow == Constants.InvalidReceiveWindow)
            {
                // No valid receive window. Abandon the message
                isMessageTooLong = true;
                return new DownlinkMessageBuilderResponse(null, isMessageTooLong, receiveWindow);
            }

            var rndToken = new byte[2];

            RndKeysGenerator.GetBytes(rndToken);

            DataRateIndex datr;
            Hertz freq;
            ushort lnsRxDelay = 0;

            var deviceJoinInfo = request.Region.LoRaRegion == LoRaRegionType.CN470RP2
                ? new DeviceJoinInfo(loRaDevice.ReportedCN470JoinChannel, loRaDevice.DesiredCN470JoinChannel)
                : null;

            if (loRaRegion is DwellTimeLimitedRegion someRegion)
                someRegion.UseDwellTimeSetting(loRaDevice.ReportedDwellTimeSetting);

            if (receiveWindow == Constants.ReceiveWindow2)
            {
                lnsRxDelay = (ushort)timeWatcher.GetReceiveWindow2Delay(loRaDevice);
                freq = loRaRegion.GetDownstreamRX2Freq(configuration.Rx2Frequency, logger, deviceJoinInfo);
                datr = loRaRegion.GetDownstreamRX2DataRate(configuration.Rx2DataRate, loRaDevice.ReportedRX2DataRate, logger, deviceJoinInfo);
            }
            else
            {
                datr = loRaRegion.GetDownstreamDataRate(radioMetadata.DataRate, loRaDevice.ReportedRX1DROffset);

                // The logic for passing CN470 join channel will change as part of #561
                if (!loRaRegion.TryGetDownstreamChannelFrequency(radioMetadata.Frequency, out freq, deviceJoinInfo: deviceJoinInfo))
                {
                    logger.LogError("there was a problem in setting the frequency in the downstream message packet forwarder settings");
                    return new DownlinkMessageBuilderResponse(null, false, receiveWindow);
                }

                lnsRxDelay = (ushort)timeWatcher.GetReceiveWindow1Delay(loRaDevice);
            }

            // get max. payload size based on data rate from LoRaRegion
            var maxPayloadSize = loRaRegion.GetMaxPayloadSize(datr);

            // Deduct 8 bytes from max payload size.
            maxPayloadSize -= Constants.LoraProtocolOverheadSize;

            var availablePayloadSize = maxPayloadSize;

            var macCommands = new List<MacCommand>();

            FramePort? fport = null;
            var requiresDeviceAcknowlegement = false;
            var macCommandType = Cid.Zero;

            byte[] frmPayload = null;

            if (cloudToDeviceMessage != null)
            {
                // Get C2D Mac coomands
                var macCommandsC2d = PrepareMacCommandAnswer(null, cloudToDeviceMessage.MacCommands, request, null, logger);

                // Calculate total C2D payload size
                var totalC2dSize = cloudToDeviceMessage.GetPayload()?.Length ?? 0;
                totalC2dSize += macCommandsC2d?.Sum(x => x.Length) ?? 0;

                // Total C2D payload will fit
                if (availablePayloadSize >= totalC2dSize)
                {
                    // Add frmPayload
                    frmPayload = cloudToDeviceMessage.GetPayload();

                    // Add C2D Mac commands
                    if (macCommandsC2d?.Count > 0)
                    {
                        foreach (var macCommand in macCommandsC2d)
                        {
                            macCommands.Add(macCommand);
                        }
                    }

                    // Deduct frmPayload size from available payload size, continue processing and log
                    availablePayloadSize -= (uint)totalC2dSize;

                    if (cloudToDeviceMessage.Confirmed)
                    {
                        requiresDeviceAcknowlegement = true;
                        loRaDevice.LastConfirmedC2DMessageID = cloudToDeviceMessage.MessageId ?? Constants.C2D_MSG_ID_PLACEHOLDER;
                    }

                    if (cloudToDeviceMessage.Fport.IsAppSpecific() || cloudToDeviceMessage.Fport.IsReserved())
                    {
                        fport = cloudToDeviceMessage.Fport;
                    }

                    logger.LogInformation($"cloud to device message: {((frmPayload?.Length ?? 0) == 0 ? "empty" : ConversionHelper.ByteArrayToString(frmPayload))}, id: {cloudToDeviceMessage.MessageId ?? "undefined"}, fport: {(byte)(fport ?? FramePort.MacCommand)}, confirmed: {requiresDeviceAcknowlegement}, cidType: {macCommandType}, macCommand: {macCommands.Count > 0}");
                    Array.Reverse(frmPayload);
                }
                else
                {
                    // Flag message to be abandoned and log`
                    logger.LogDebug($"cloud to device message: empty, id: {cloudToDeviceMessage.MessageId ?? "undefined"}, fport: 0, confirmed: {requiresDeviceAcknowlegement} too long for current receive window. Abandoning.");
                    isMessageTooLong = true;
                }
            }

            // Get request Mac commands
            var macCommandsRequest = PrepareMacCommandAnswer(upstreamPayload.MacCommands, null, request, loRaADRResult, logger);

            // Calculate request Mac commands size
            var macCommandsRequestSize = macCommandsRequest?.Sum(x => x.Length) ?? 0;

            // Try adding request Mac commands
            if (availablePayloadSize >= macCommandsRequestSize)
            {
                if (macCommandsRequest?.Count > 0)
                {
                    foreach (var macCommand in macCommandsRequest)
                    {
                        macCommands.Add(macCommand);
                    }
                }
            }

            if (fpending || isMessageTooLong)
            {
                fctrl |= FrameControlFlags.DownlinkFramePending;
            }

            if (upstreamPayload.IsDataRateNetworkControlled)
            {
                fctrl |= FrameControlFlags.Adr;
            }

            var srcDevAddr = upstreamPayload.DevAddr.Span;
            var reversedDevAddr = new byte[srcDevAddr.Length];
            for (var i = reversedDevAddr.Length - 1; i >= 0; --i)
            {
                reversedDevAddr[i] = srcDevAddr[^(1 + i)];
            }

            var msgType = requiresDeviceAcknowlegement ? MacMessageType.ConfirmedDataDown : MacMessageType.UnconfirmedDataDown;
            var ackLoRaMessage = new LoRaPayloadData(
                msgType,
                reversedDevAddr,
                fctrl,
                BitConverter.GetBytes(fcntDownToSend),
                macCommands,
                fport,
                frmPayload,
                1,
                loRaDevice.Supports32BitFCnt ? fcntDown : null);

            // todo: check the device twin preference if using confirmed or unconfirmed down
            var downlinkMessage = BuildDownstreamMessage(loRaDevice, request.StationEui, logger, radioMetadata.UpInfo.Xtime, datr, loRaRegion.GetDownstreamRX2DataRate(configuration.Rx2DataRate, loRaDevice.ReportedRX2DataRate, logger, deviceJoinInfo), freq, loRaRegion.GetDownstreamRX2Freq(configuration.Rx2Frequency, logger, deviceJoinInfo), lnsRxDelay, ackLoRaMessage, radioMetadata.UpInfo.AntennaPreference);

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug($"{ackLoRaMessage.MessageType} {JsonConvert.SerializeObject(downlinkMessage)}");

            return new DownlinkMessageBuilderResponse(downlinkMessage, isMessageTooLong, receiveWindow);
        }

        private static DownlinkMessage BuildDownstreamMessage(LoRaDevice loRaDevice, StationEui stationEUI, ILogger logger, ulong xTime, DataRateIndex rx1Datr, DataRateIndex rx2Datr, Hertz freqRx1, Hertz freqRx2, ushort lnsRxDelay, LoRaPayloadData loRaMessage, uint? antennaPreference = null)
        {
            var messageBytes = loRaMessage.Serialize(loRaDevice.AppSKey, loRaDevice.NwkSKey);
            var downlinkMessage = new DownlinkMessage(
                messageBytes,
                xTime,
                rx1Datr,
                rx2Datr,
                freqRx1,
                freqRx2,
                DevEui.Parse(loRaDevice.DevEUI),
                lnsRxDelay,
                stationEUI,
                antennaPreference
                );

            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug($"{loRaMessage.MessageType} {JsonConvert.SerializeObject(downlinkMessage)}");
            return downlinkMessage;
        }

        private static ushort ValidateAndConvert16bitFCnt(uint fcntDown)
        {
            if (fcntDown == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fcntDown));
            }

            return (ushort)fcntDown;
        }

        internal static DownlinkMessageBuilderResponse CreateDownlinkMessage(
            NetworkServerConfiguration configuration,
            LoRaDevice loRaDevice,
            Region loRaRegion,
            IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage,
            uint fcntDown,
            ILogger logger)
        {
            var fcntDownToSend = ValidateAndConvert16bitFCnt(fcntDown);

            // default fport
            var macCommandType = Cid.Zero;

            var rndToken = new byte[2];
            RndKeysGenerator.GetBytes(rndToken);

            var isMessageTooLong = false;

            // Class C always uses RX2
            DataRateIndex datr;
            Hertz freq;

            // Class C always use RX2
            freq = loRaRegion.GetDownstreamRX2Freq(configuration.Rx2Frequency, logger);
            datr = loRaRegion.GetDownstreamRX2DataRate(configuration.Rx2DataRate, loRaDevice.ReportedRX2DataRate, logger);

            // get max. payload size based on data rate from LoRaRegion
            var maxPayloadSize = loRaRegion.GetMaxPayloadSize(datr);

            // Deduct 8 bytes from max payload size.
            maxPayloadSize -= Constants.LoraProtocolOverheadSize;

            var availablePayloadSize = maxPayloadSize;

            var macCommands = PrepareMacCommandAnswer(null, cloudToDeviceMessage.MacCommands, null, null, logger);

            // Calculate total C2D payload size
            var totalC2dSize = cloudToDeviceMessage.GetPayload()?.Length ?? 0;
            totalC2dSize += macCommands?.Sum(x => x.Length) ?? 0;

            // Total C2D payload will NOT fit
            if (availablePayloadSize < totalC2dSize)
            {
                isMessageTooLong = true;
                return new DownlinkMessageBuilderResponse(null, isMessageTooLong, Constants.ReceiveWindow2);
            }

            if (macCommands?.Count > 0)
            {
                macCommandType = macCommands.First().Cid;
            }

            if (cloudToDeviceMessage.Confirmed)
            {
                loRaDevice.LastConfirmedC2DMessageID = cloudToDeviceMessage.MessageId ?? Constants.C2D_MSG_ID_PLACEHOLDER;
            }

            var frmPayload = cloudToDeviceMessage.GetPayload();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation($"cloud to device message: {ConversionHelper.ByteArrayToString(frmPayload)}, id: {cloudToDeviceMessage.MessageId ?? "undefined"}, fport: {cloudToDeviceMessage.Fport}, confirmed: {cloudToDeviceMessage.Confirmed}, cidType: {macCommandType}");
            }

            Array.Reverse(frmPayload);

            var payloadDevAddr = ConversionHelper.StringToByteArray(loRaDevice.DevAddr);
            var reversedDevAddr = new byte[payloadDevAddr.Length];
            for (var i = reversedDevAddr.Length - 1; i >= 0; --i)
            {
                reversedDevAddr[i] = payloadDevAddr[^(1 + i)];
            }

            var msgType = cloudToDeviceMessage.Confirmed ? MacMessageType.ConfirmedDataDown : MacMessageType.UnconfirmedDataDown;
            var ackLoRaMessage = new LoRaPayloadData(
                msgType,
                reversedDevAddr,
                FrameControlFlags.None,
                BitConverter.GetBytes(fcntDownToSend),
                macCommands,
                cloudToDeviceMessage.Fport,
                frmPayload,
                1,
                loRaDevice.Supports32BitFCnt ? fcntDown : null);

            var loraDownLinkMessage = BuildDownstreamMessage(loRaDevice, loRaDevice.LastProcessingStationEui, logger, 0, datr, datr, freq, freq, 0, ackLoRaMessage);
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug($"{ackLoRaMessage.MessageType} {JsonConvert.SerializeObject(loraDownLinkMessage)}");

            // Class C always uses RX2.
            return new DownlinkMessageBuilderResponse(loraDownLinkMessage, isMessageTooLong, Constants.ReceiveWindow2);
        }

        /// <summary>
        /// Prepare the Mac Commands to be sent in the downstream message.
        /// </summary>
        private static ICollection<MacCommand> PrepareMacCommandAnswer(
            IEnumerable<MacCommand> requestedMacCommands,
            IEnumerable<MacCommand> serverMacCommands,
            LoRaRequest loRaRequest,
            LoRaADRResult loRaADRResult,
            ILogger logger)
        {
            var cids = new HashSet<Cid>();
            var macCommands = new List<MacCommand>();

            if (requestedMacCommands != null)
            {
                foreach (var requestedMacCommand in requestedMacCommands)
                {
                    switch (requestedMacCommand.Cid)
                    {
                        case Cid.LinkCheckCmd:
                        case Cid.Zero:
                        case Cid.One:
                        case Cid.LinkADRCmd:
                            if (loRaRequest != null)
                            {
                                var linkCheckAnswer = new LinkCheckAnswer(loRaRequest.Region.GetModulationMargin(loRaRequest.RadioMetadata.DataRate, loRaRequest.RadioMetadata.UpInfo.SignalNoiseRatio), 1);
                                if (cids.Add(Cid.LinkCheckCmd))
                                {
                                    macCommands.Add(linkCheckAnswer);
                                    logger.LogInformation($"answering to a MAC command request {linkCheckAnswer}");
                                }
                            }
                            break;
                        case Cid.DutyCycleCmd:
                        case Cid.RXParamCmd:
                        case Cid.DevStatusCmd:
                        case Cid.NewChannelCmd:
                        case Cid.RXTimingCmd:
                        case Cid.TxParamSetupCmd:
                        default:
                            break;
                    }
                }
            }

            if (serverMacCommands != null)
            {
                foreach (var macCmd in serverMacCommands)
                {
                    if (macCmd != null)
                    {
                        try
                        {
                            if (cids.Add(macCmd.Cid))
                            {
                                macCommands.Add(macCmd);
                            }
                            else
                            {
                                logger.LogError($"could not send the cloud to device MAC command {macCmd.Cid}, as such a property was already present in the message. Please resend the cloud to device message");
                            }

                            logger.LogInformation($"cloud to device MAC command {macCmd.Cid} received {macCmd}");
                        }
                        catch (MacCommandException ex) when (ExceptionFilterUtility.True(() => logger.LogError(ex.ToString())))
                        {
                            // continue
                        }
                    }
                }
            }

            // ADR Part.
            // Currently only replying on ADR Req
            if (loRaADRResult?.CanConfirmToDevice == true)
            {
                const int placeholderChannel = 25;
                var linkADR = new LinkADRRequest((byte)loRaADRResult.DataRate, (byte)loRaADRResult.TxPower, placeholderChannel, 0, (byte)loRaADRResult.NbRepetition);
                macCommands.Add(linkADR);
                logger.LogInformation($"performing a rate adaptation: DR {loRaADRResult.DataRate}, transmit power {loRaADRResult.TxPower}, #repetition {loRaADRResult.NbRepetition}");
            }

            return macCommands;
        }
    }
}
