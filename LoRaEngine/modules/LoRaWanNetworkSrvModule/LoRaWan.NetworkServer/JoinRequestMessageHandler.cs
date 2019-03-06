// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;

    public class JoinRequestMessageHandler
    {
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly NetworkServerConfiguration configuration;

        public JoinRequestMessageHandler(NetworkServerConfiguration configuration, ILoRaDeviceRegistry deviceRegistry)
        {
            this.deviceRegistry = deviceRegistry;
            this.configuration = configuration;
        }

        public void DispatchRequest(LoRaRequest request)
        {
            Task.Run(async () => await this.ProcessJoinRequestAsync(request));
        }

        /// <summary>
        /// Process OTAA join request
        /// </summary>
        async Task ProcessJoinRequestAsync(LoRaRequest request)
        {
            LoRaDevice loRaDevice = null;
            string devEUI = null;
            var loraRegion = request.LoRaRegion;

            try
            {
                var timeWatcher = new LoRaOperationTimeWatcher(loraRegion, request.StartTime);

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

                var oldDevAddr = loRaDevice.DevAddr;

                if (!timeWatcher.InTimeForJoinAccept())
                {
                    // in this case it's too late, we need to break and avoid saving twins
                    Logger.Log(devEUI, $"join refused: processing of the join request took too long, sending no message", LogLevel.Information);
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                Logger.Log(loRaDevice.DevEUI, $"saving join properties twins", LogLevel.Debug);
                var deviceUpdateSucceeded = await loRaDevice.UpdateAfterJoinAsync(
                    devAddr,
                    nwkSKey,
                    appSKey,
                    appNonce,
                    devNonce,
                    ConversionHelper.ByteArrayToString(netId),
                    request.LoRaRegion.LoRaRegion,
                    this.configuration.GatewayID);

                Logger.Log(loRaDevice.DevEUI, $"done saving join properties twins", LogLevel.Debug);

                if (!deviceUpdateSucceeded)
                {
                    Logger.Log(devEUI, $"join refused: join request could not save twins", LogLevel.Error);
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.ApplicationError);
                    return;
                }

                var windowToUse = timeWatcher.ResolveJoinAcceptWindowToUse(loRaDevice);
                if (windowToUse == Constants.INVALID_RECEIVE_WINDOW)
                {
                    Logger.Log(devEUI, $"join refused: processing of the join request took too long, sending no message", LogLevel.Information);
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                double freq = 0;
                string datr = null;
                uint tmst = 0;
                if (windowToUse == Constants.RECEIVE_WINDOW_1)
                {
                    datr = loraRegion.GetDownstreamDR(request.Rxpk);
                    freq = loraRegion.GetDownstreamChannelFrequency(request.Rxpk);

                    // set tmst for the normal case
                    tmst = request.Rxpk.Tmst + loraRegion.Join_accept_delay1 * 1000000;
                }
                else
                {
                    Logger.Log(devEUI, $"processing of the join request took too long, using second join accept receive window", LogLevel.Information);
                    tmst = request.Rxpk.Tmst + loraRegion.Join_accept_delay2 * 1000000;

                    (freq, datr) = loraRegion.GetDownstreamRX2DRAndFreq(devEUI, this.configuration.Rx2DataRate, this.configuration.Rx2DataFrequency, null);
                }

                loRaDevice.IsOurDevice = true;
                await this.deviceRegistry.UpdateDeviceAfterJoinAsync(loRaDevice, oldDevAddr);

                // Build join accept downlink message
                Array.Reverse(netId);
                Array.Reverse(appNonceBytes);

                // Build the DlSettings fields that is a superposition of RX2DR and RX1DROffset field
                byte[] dlSettings = new byte[1];

                if (request.LoRaRegion.RegionLimits.IsCurrentDRIndexWithinAcceptableValue(loRaDevice.DesiredRX2DataRate))
                {
                    dlSettings[0] =
                        (byte)(loRaDevice.DesiredRX2DataRate & 0b00001111);
                }
                else
                {
                    Logger.Log(devEUI, $"twin RX2 datarate values are not within acceptable values", LogLevel.Error);
                }

                if (loRaDevice.DesiredRX1DROffset >= 0 && loRaDevice.DesiredRX1DROffset < request.LoRaRegion.RX1DROffsetTable.GetUpperBound(1))
                {
                    var rx1droffset = (byte)(loRaDevice.DesiredRX1DROffset << 4);
                    dlSettings[0] = (byte)(dlSettings[0] + rx1droffset);
                }
                else
                {
                    Logger.Log(devEUI, $"twin Rx1 offset datarate values are not within acceptable values", LogLevel.Error);
                }

                var joinAccept = this.CreateJoinAcceptDownlinkMessage(
                    netId,
                    loRaDevice.AppKey,
                    devAddr,
                    appNonceBytes,
                    datr,
                    freq,
                    tmst,
                    devEUI,
                    dlSettings);

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
            string devEUI,
            byte[] dlSettings)
        {
            var loRaPayloadJoinAccept = new LoRaTools.LoRaMessage.LoRaPayloadJoinAccept(
                LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId), // NETID 0 / 1 is default test
                ConversionHelper.StringToByteArray(devAddr), // todo add device address management
                appNonce.ToArray(),
                dlSettings,
                new byte[] { 0 },
                null);

            return loRaPayloadJoinAccept.Serialize(appKey, datr, freq, tmst, devEUI);
        }
    }
}