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

    /// <summary>
    /// Helper class to create <see cref="DownlinkPktFwdMessage"/>.
    /// </summary>
    internal static class DownlinkMessageBuilder
    {
        private static readonly RandomNumberGenerator RndKeysGenerator = new RNGCryptoServiceProvider();

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
            LoRaADRResult loRaADRResult)
        {
            var fcntDownToSend = ValidateAndConvert16bitFCnt(fcntDown);

            var upstreamPayload = (LoRaPayloadData)request.Payload;
            var rxpk = request.Rxpk;
            var loRaRegion = request.Region;
            var isMessageTooLong = false;

            // default fport
            byte fctrl = 0;
            if (upstreamPayload.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
            {
                // Confirm receiving message to device
                fctrl = (byte)Fctrl.Ack;
            }

            // Calculate receive window
            var receiveWindow = timeWatcher.ResolveReceiveWindowToUse(loRaDevice);
            if (receiveWindow == Constants.InvalidReceiveWindow)
            {
                // No valid receive window. Abandon the message
                isMessageTooLong = true;
                return new DownlinkMessageBuilderResponse(null, isMessageTooLong);
            }

            var rndToken = new byte[2];

            RndKeysGenerator.GetBytes(rndToken);

            string datr;
            double freq;
            long tmst;
            ushort lnsRxDelay = 0;

            var deviceJoinInfo = request.Region.LoRaRegion == LoRaRegionType.CN470
                ? new DeviceJoinInfo(loRaDevice.ReportedCN470JoinChannel, loRaDevice.DesiredCN470JoinChannel)
                : null;

            if (receiveWindow == Constants.ReceiveWindow2)
            {
                lnsRxDelay = (ushort)timeWatcher.GetReceiveWindow2Delay(loRaDevice);
                tmst = rxpk.Tmst + (timeWatcher.GetReceiveWindow2Delay(loRaDevice) * Constants.ConvertToPktFwdTime);
                freq = loRaRegion.GetDownstreamRX2Freq(loRaDevice.DevEUI, configuration.Rx2Frequency, deviceJoinInfo);
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                datr = loRaRegion.GetDownstreamRX2DataRate(loRaDevice.DevEUI, configuration.Rx2DataRate, loRaDevice.ReportedRX2DataRate, deviceJoinInfo);
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
            }
            else
            {
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                datr = loRaRegion.GetDownstreamDataRate(rxpk, loRaDevice.ReportedRX1DROffset);
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                if (datr == null)
                {
                    StaticLogger.Log(loRaDevice.DevEUI, "there was a problem in setting the data rate in the downstream message packet forwarder settings", LogLevel.Error);
                    return new DownlinkMessageBuilderResponse(null, false);
                }

                // The logic for passing CN470 join channel will change as part of #561
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                if (!loRaRegion.TryGetDownstreamChannelFrequency(rxpk, out freq, deviceJoinInfo))
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                {
                    StaticLogger.Log(loRaDevice.DevEUI, "there was a problem in setting the frequency in the downstream message packet forwarder settings", LogLevel.Error);
                    return new DownlinkMessageBuilderResponse(null, false);
                }

                tmst = rxpk.Tmst + (timeWatcher.GetReceiveWindow1Delay(loRaDevice) * Constants.ConvertToPktFwdTime);
                lnsRxDelay = (ushort)timeWatcher.GetReceiveWindow1Delay(loRaDevice);
            }

            // get max. payload size based on data rate from LoRaRegion
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
            var maxPayloadSize = loRaRegion.GetMaxPayloadSize(datr);
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done

            // Deduct 8 bytes from max payload size.
            maxPayloadSize -= Constants.LoraProtocolOverheadSize;

            var availablePayloadSize = maxPayloadSize;

            var macCommands = new List<MacCommand>();

            byte? fport = null;
            var requiresDeviceAcknowlegement = false;
            var macCommandType = Cid.Zero;

            byte[] frmPayload = null;

            if (cloudToDeviceMessage != null)
            {
                // Get C2D Mac coomands
                var macCommandsC2d = PrepareMacCommandAnswer(loRaDevice.DevEUI, null, cloudToDeviceMessage.MacCommands, rxpk, null);

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

                    if (cloudToDeviceMessage.Fport > 0)
                    {
                        fport = cloudToDeviceMessage.Fport;
                    }

                    StaticLogger.Log(loRaDevice.DevEUI, $"cloud to device message: {((frmPayload?.Length ?? 0) == 0 ? "empty" : ConversionHelper.ByteArrayToString(frmPayload))}, id: {cloudToDeviceMessage.MessageId ?? "undefined"}, fport: {fport ?? 0}, confirmed: {requiresDeviceAcknowlegement}, cidType: {macCommandType}, macCommand: {macCommands.Count > 0}", LogLevel.Information);
                    Array.Reverse(frmPayload);
                }
                else
                {
                    // Flag message to be abandoned and log
                    StaticLogger.Log(loRaDevice.DevEUI, $"cloud to device message: empty, id: {cloudToDeviceMessage.MessageId ?? "undefined"}, fport: 0, confirmed: {requiresDeviceAcknowlegement} too long for current receive window. Abandoning.", LogLevel.Debug);
                    isMessageTooLong = true;
                }
            }

            // Get request Mac commands
            var macCommandsRequest = PrepareMacCommandAnswer(loRaDevice.DevEUI, upstreamPayload.MacCommands, null, rxpk, loRaADRResult);

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
                fctrl |= (int)Fctrl.FpendingOrClassB;
            }

            if (upstreamPayload.IsAdrEnabled)
            {
                fctrl |= (byte)Fctrl.ADR;
            }

            var srcDevAddr = upstreamPayload.DevAddr.Span;
            var reversedDevAddr = new byte[srcDevAddr.Length];
            for (var i = reversedDevAddr.Length - 1; i >= 0; --i)
            {
                reversedDevAddr[i] = srcDevAddr[^(1 + i)];
            }

            var msgType = requiresDeviceAcknowlegement ? LoRaMessageType.ConfirmedDataDown : LoRaMessageType.UnconfirmedDataDown;
            var ackLoRaMessage = new LoRaPayloadData(
                msgType,
                reversedDevAddr,
                new byte[] { fctrl },
                BitConverter.GetBytes(fcntDownToSend),
                macCommands,
                fport.HasValue ? new byte[] { fport.Value } : null,
                frmPayload,
                1,
                loRaDevice.Supports32BitFCnt ? fcntDown : null);

            // todo: check the device twin preference if using confirmed or unconfirmed down
            StaticLogger.Log(loRaDevice.DevEUI, $"sending a downstream message with ID {ConversionHelper.ByteArrayToString(rndToken)}", LogLevel.Information);
            return new DownlinkMessageBuilderResponse(
                ackLoRaMessage.Serialize(loRaDevice.AppSKey, loRaDevice.NwkSKey, datr, freq, tmst, loRaDevice.DevEUI, lnsRxDelay, rxpk.Rfch, rxpk.Time, request.StationEui),
                isMessageTooLong);
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
            uint fcntDown)
        {
            var fcntDownToSend = ValidateAndConvert16bitFCnt(fcntDown);

            // default fport
            byte fctrl = 0;
            var macCommandType = Cid.Zero;

            var rndToken = new byte[2];
            RndKeysGenerator.GetBytes(rndToken);

            var isMessageTooLong = false;

            // Class C always uses RX2
            string datr;
            double freq;
            var tmst = 0; // immediate mode
            ushort rxDelay = 0; // Class C sends immediately

            // Class C always use RX2
            freq = loRaRegion.GetDownstreamRX2Freq(loRaDevice.DevEUI, configuration.Rx2Frequency);
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
            datr = loRaRegion.GetDownstreamRX2DataRate(loRaDevice.DevEUI, configuration.Rx2DataRate, loRaDevice.ReportedRX2DataRate);
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done

            // get max. payload size based on data rate from LoRaRegion
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
            var maxPayloadSize = loRaRegion.GetMaxPayloadSize(datr);
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done

            // Deduct 8 bytes from max payload size.
            maxPayloadSize -= Constants.LoraProtocolOverheadSize;

            var availablePayloadSize = maxPayloadSize;

            var macCommands = PrepareMacCommandAnswer(loRaDevice.DevEUI, null, cloudToDeviceMessage.MacCommands, null, null);

            // Calculate total C2D payload size
            var totalC2dSize = cloudToDeviceMessage.GetPayload()?.Length ?? 0;
            totalC2dSize += macCommands?.Sum(x => x.Length) ?? 0;

            // Total C2D payload will NOT fit
            if (availablePayloadSize < totalC2dSize)
            {
                isMessageTooLong = true;
                return new DownlinkMessageBuilderResponse(null, isMessageTooLong);
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

            if (StaticLogger.LoggerLevel <= LogLevel.Information)
            {
                StaticLogger.Log(loRaDevice.DevEUI, $"cloud to device message: {ConversionHelper.ByteArrayToString(frmPayload)}, id: {cloudToDeviceMessage.MessageId ?? "undefined"}, fport: {cloudToDeviceMessage.Fport}, confirmed: {cloudToDeviceMessage.Confirmed}, cidType: {macCommandType}", LogLevel.Information);
                StaticLogger.Log(loRaDevice.DevEUI, $"sending a downstream message with ID {ConversionHelper.ByteArrayToString(rndToken)}", LogLevel.Information);
            }

            Array.Reverse(frmPayload);

            var payloadDevAddr = ConversionHelper.StringToByteArray(loRaDevice.DevAddr);
            var reversedDevAddr = new byte[payloadDevAddr.Length];
            for (var i = reversedDevAddr.Length - 1; i >= 0; --i)
            {
                reversedDevAddr[i] = payloadDevAddr[^(1 + i)];
            }

            var msgType = cloudToDeviceMessage.Confirmed ? LoRaMessageType.ConfirmedDataDown : LoRaMessageType.UnconfirmedDataDown;
            var ackLoRaMessage = new LoRaPayloadData(
                msgType,
                reversedDevAddr,
                new byte[] { fctrl },
                BitConverter.GetBytes(fcntDownToSend),
                macCommands,
                new byte[] { cloudToDeviceMessage.Fport },
                frmPayload,
                1,
                loRaDevice.Supports32BitFCnt ? fcntDown : null);

            return new DownlinkMessageBuilderResponse(
                ackLoRaMessage.Serialize(loRaDevice.AppSKey,
                                         loRaDevice.NwkSKey,
                                         datr,
                                         freq,
                                         tmst,
                                         loRaDevice.DevEUI,
                                         rxDelay,
                                         stationEui: loRaDevice.LastProcessingStationEui),
                isMessageTooLong);
        }

        /// <summary>
        /// Prepare the Mac Commands to be sent in the downstream message.
        /// </summary>
        private static ICollection<MacCommand> PrepareMacCommandAnswer(
            string devEUI,
            IEnumerable<MacCommand> requestedMacCommands,
            IEnumerable<MacCommand> serverMacCommands,
            Rxpk rxpk,
            LoRaADRResult loRaADRResult)
        {
            var macCommands = new Dictionary<int, MacCommand>();

            if (requestedMacCommands != null)
            {
                foreach (var requestedMacCommand in requestedMacCommands)
                {
                    switch (requestedMacCommand.Cid)
                    {
                        case Cid.LinkCheckCmd:
                        {
                            if (rxpk != null)
                            {
                                var linkCheckAnswer = new LinkCheckAnswer(rxpk.GetModulationMargin(), 1);
                                if (macCommands.TryAdd((int)Cid.LinkCheckCmd, linkCheckAnswer))
                                {
                                    StaticLogger.Log(devEUI, $"answering to a MAC command request {linkCheckAnswer}", LogLevel.Information);
                                }
                            }

                            break;
                        }
                        case Cid.Zero:
                        case Cid.One:
                        case Cid.LinkADRCmd:
                        case Cid.DutyCycleCmd:
                        case Cid.RXParamCmd:
                        case Cid.DevStatusCmd:
                        case Cid.NewChannelCmd:
                        case Cid.RXTimingCmd:
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
                            if (!macCommands.TryAdd((int)macCmd.Cid, macCmd))
                            {
                                StaticLogger.Log(devEUI, $"could not send the cloud to device MAC command {macCmd.Cid}, as such a property was already present in the message. Please resend the cloud to device message", LogLevel.Error);
                            }

                            StaticLogger.Log(devEUI, $"cloud to device MAC command {macCmd.Cid} received {macCmd}", LogLevel.Information);
                        }
                        catch (MacCommandException ex)
                        {
                            StaticLogger.Log(devEUI, ex.ToString(), LogLevel.Error);
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
                macCommands.Add((int)Cid.LinkADRCmd, linkADR);
                StaticLogger.Log(devEUI, $"performing a rate adaptation: DR {loRaADRResult.DataRate}, transmit power {loRaADRResult.TxPower}, #repetition {loRaADRResult.NbRepetition}", LogLevel.Information);
            }

            return macCommands.Values;
        }
    }
}
