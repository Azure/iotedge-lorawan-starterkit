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
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Message dispatcher
    /// </summary>
    public class MessageDispatcher
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

        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory;
        private volatile Region loraRegion;

        public MessageDispatcher(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry deviceRegistry,
            ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory)
        {
            this.configuration = configuration;
            this.deviceRegistry = deviceRegistry;
            this.frameCounterUpdateStrategyFactory = frameCounterUpdateStrategyFactory;

            // Register frame counter initializer
            // It will take care of seeding ABP devices created here for single gateway scenarios
            this.deviceRegistry.RegisterDeviceInitializer(new FrameCounterLoRaDeviceInitializer(configuration.GatewayID, frameCounterUpdateStrategyFactory));
        }

        /// <summary>
        /// Dispatches a request
        /// </summary>
        public void DispatchRequest(LoRaRequest request)
        {
            if (!LoRaPayload.TryCreateLoRaPayload(request.Rxpk, out LoRaPayload loRaPayload))
            {
                Logger.Log("There was a problem in decoding the Rxpk", LogLevel.Error);
                request.NotifyFailed(null, LoRaDeviceRequestFailedReason.InvalidRxpk);
                return;
            }

            if (this.loraRegion == null)
            {
                if (!RegionFactory.TryResolveRegion(request.Rxpk))
                {
                    // log is generated in Region factory
                    // move here once V2 goes GA
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.InvalidRegion);
                    return;
                }

                this.loraRegion = RegionFactory.CurrentRegion;
            }

            request.SetPayload(loRaPayload);
            request.SetRegion(this.loraRegion);

            var loggingRequest = new LoggingLoRaRequest(request);

            if (loRaPayload.LoRaMessageType == LoRaMessageType.JoinRequest)
            {
                _ = this.ProcessJoinRequestAsync(loggingRequest);
            }
            else if (loRaPayload.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp || loRaPayload.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
            {
                this.DispatchLoRaDataMessage(loggingRequest);
            }
            else
            {
                Logger.Log("Unknwon message type in rxpk, message ignored", LogLevel.Error);
            }
        }

        void DispatchLoRaDataMessage(LoRaRequest request)
        {
            var loRaPayload = (LoRaPayloadData)request.Payload;
            if (!this.IsValidNetId(loRaPayload.GetDevAddrNetID(), this.configuration.NetId))
            {
                Logger.Log(ConversionHelper.ByteArrayToString(loRaPayload.DevAddr), $"device is using another network id, ignoring this message (network: {this.configuration.NetId}, devAddr: {loRaPayload.GetDevAddrNetID()})", LogLevel.Debug);
                request.NotifyFailed(null, LoRaDeviceRequestFailedReason.InvalidNetId);
                return;
            }

            this.deviceRegistry.GetLoRaRequestQueue(request).Queue(request);
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
        async Task ProcessJoinRequestAsync(LoRaRequest request)
        {
            LoRaDevice loRaDevice = null;
            string devEUI = null;

            try
            {
                var timeWatcher = new LoRaOperationTimeWatcher(this.loraRegion, request.StartTime);

                var joinReq = (LoRaPayloadJoinRequest)request.Payload;
                byte[] udpMsgForPktForwarder = new byte[0];

                devEUI = joinReq.GetDevEUIAsString();
                var appEUI = joinReq.GetAppEUIAsString();

                var devNonce = joinReq.GetDevNonceAsString();
                Logger.Log(devEUI, $"join request received", LogLevel.Information);

                loRaDevice = await this.deviceRegistry.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce);
                if (loRaDevice == null)
                {
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.UnknownDevice);
                    return;
                }

                if (string.IsNullOrEmpty(loRaDevice.AppKey))
                {
                    Logger.Log(loRaDevice.DevEUI, "join refused: missing AppKey for OTAA device", LogLevel.Error);
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.InvalidJoinRequest);
                    return;
                }

                if (loRaDevice.AppEUI != appEUI)
                {
                    Logger.Log(devEUI, "join refused: AppEUI for OTAA does not match device", LogLevel.Error);
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.InvalidJoinRequest);
                    return;
                }

                if (!joinReq.CheckMic(loRaDevice.AppKey))
                {
                    Logger.Log(devEUI, "join refused: invalid MIC", LogLevel.Error);
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.JoinMicCheckFailed);
                    return;
                }

                // Make sure that is a new request and not a replay
                if (!string.IsNullOrEmpty(loRaDevice.DevNonce) && loRaDevice.DevNonce == devNonce)
                {
                    Logger.Log(devEUI, "join refused: DevNonce already used by this device", LogLevel.Information);
                    loRaDevice.IsOurDevice = false;
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.JoinDevNonceAlreadyUsed);
                    return;
                }

                // Check that the device is joining through the linked gateway and not another
                if (!string.IsNullOrEmpty(loRaDevice.GatewayID) && !string.Equals(loRaDevice.GatewayID, this.configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase))
                {
                    Logger.Log(devEUI, $"join refused: trying to join not through its linked gateway, ignoring join request", LogLevel.Information);
                    loRaDevice.IsOurDevice = false;
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.HandledByAnotherGateway);
                    return;
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
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                Logger.Log(loRaDevice.DevEUI, $"saving join properties twins", LogLevel.Debug);
                var deviceUpdateSucceeded = await loRaDevice.UpdateAfterJoinAsync(devAddr, nwkSKey, appSKey, appNonce, devNonce, LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId));
                Logger.Log(loRaDevice.DevEUI, $"done saving join properties twins", LogLevel.Debug);

                if (!deviceUpdateSucceeded)
                {
                    Logger.Log(devEUI, $"join refused: join request could not save twins", LogLevel.Error);
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.ApplicationError);
                    return;
                }

                var windowToUse = timeWatcher.ResolveJoinAcceptWindowToUse(loRaDevice);
                if (windowToUse == 0)
                {
                    Logger.Log(devEUI, $"join refused: processing of the join request took too long, sending no message", LogLevel.Information);
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                double freq = 0;
                string datr = null;
                uint tmst = 0;
                if (windowToUse == 1)
                {
                    datr = this.loraRegion.GetDownstreamDR(request.Rxpk);
                    freq = this.loraRegion.GetDownstreamChannelFrequency(request.Rxpk);

                    // set tmst for the normal case
                    tmst = request.Rxpk.Tmst + this.loraRegion.Join_accept_delay1 * 1000000;
                }
                else
                {
                    Logger.Log(devEUI, $"processing of the join request took too long, using second join accept receive window", LogLevel.Information);
                    tmst = request.Rxpk.Tmst + this.loraRegion.Join_accept_delay2 * 1000000;
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

                var joinAccept = this.CreateJoinAcceptDownlinkMessage(
                    netId,
                    loRaDevice.AppKey,
                    devAddr,
                    appNonceBytes,
                    datr,
                    freq,
                    tmst,
                    devEUI);

                if (joinAccept != null)
                {
                    _ = request.PacketForwarder.SendDownstreamAsync(joinAccept);
                    request.NotifySucceeded(loRaDevice, joinAccept);
                }
            }
            catch (Exception ex)
            {
                var deviceId = devEUI ?? ConversionHelper.ByteArrayToString(request.Payload.DevAddr);
                Logger.Log(deviceId, $"Failed to handle join request. {ex.Message}", LogLevel.Error);
                request.NotifyFailed(loRaDevice, ex);
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
    }
}
