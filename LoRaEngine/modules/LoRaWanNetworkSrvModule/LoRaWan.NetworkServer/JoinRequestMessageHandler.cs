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
    using Newtonsoft.Json;

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
            var loraRegion = request.Region;

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
                    request.NotifyFailed(devEUI, LoRaDeviceRequestFailedReason.UnknownDevice);
                    // we do not log here as we assume that the deviceRegistry does a more informed logging if returning null
                    return;
                }

                if (string.IsNullOrEmpty(loRaDevice.AppKey))
                {
                    Logger.Log(loRaDevice.DevEUI, "join refused: missing AppKey for OTAA device", LogLevel.Error);
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidJoinRequest);
                    return;
                }

                if (loRaDevice.AppEUI != appEUI)
                {
                    Logger.Log(devEUI, "join refused: AppEUI for OTAA does not match device", LogLevel.Error);
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidJoinRequest);
                    return;
                }

                if (!joinReq.CheckMic(loRaDevice.AppKey))
                {
                    Logger.Log(devEUI, "join refused: invalid MIC", LogLevel.Error);
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.JoinMicCheckFailed);
                    return;
                }

                // Make sure that is a new request and not a replay
                if (!string.IsNullOrEmpty(loRaDevice.DevNonce) && loRaDevice.DevNonce == devNonce)
                {
                    if (string.IsNullOrEmpty(loRaDevice.GatewayID))
                    {
                        Logger.Log(devEUI, "join refused: join already processed by another gateway", LogLevel.Information);
                    }
                    else
                    {
                        Logger.Log(devEUI, "join refused: DevNonce already used by this device", LogLevel.Error);
                    }

                    loRaDevice.IsOurDevice = false;
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.JoinDevNonceAlreadyUsed);
                    return;
                }

                // Check that the device is joining through the linked gateway and not another
                if (!string.IsNullOrEmpty(loRaDevice.GatewayID) && !string.Equals(loRaDevice.GatewayID, this.configuration.GatewayID, StringComparison.InvariantCultureIgnoreCase))
                {
                    Logger.Log(devEUI, $"join refused: trying to join not through its linked gateway, ignoring join request", LogLevel.Information);
                    loRaDevice.IsOurDevice = false;
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.HandledByAnotherGateway);
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
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                var updatedProperties = new LoRaDeviceJoinUpdateProperties
                {
                    DevAddr = devAddr,
                    NwkSKey = nwkSKey,
                    AppSKey = appSKey,
                    AppNonce = appNonce,
                    DevNonce = devNonce,
                    NetID = ConversionHelper.ByteArrayToString(netId),
                    Region = request.Region.LoRaRegion,
                    PreferredGatewayID = this.configuration.GatewayID,
                };

                if (loRaDevice.ClassType == LoRaDeviceClassType.C)
                {
                    updatedProperties.SavePreferredGateway = true;
                    updatedProperties.SaveRegion = true;
                }

                var deviceUpdateSucceeded = await loRaDevice.UpdateAfterJoinAsync(updatedProperties);

                if (!deviceUpdateSucceeded)
                {
                    Logger.Log(devEUI, $"join refused: join request could not save twin", LogLevel.Error);
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ApplicationError);
                    return;
                }

                var windowToUse = timeWatcher.ResolveJoinAcceptWindowToUse(loRaDevice);
                if (windowToUse == Constants.INVALID_RECEIVE_WINDOW)
                {
                    Logger.Log(devEUI, $"join refused: processing of the join request took too long, sending no message", LogLevel.Information);
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                double freq = 0;
                string datr = null;
                uint tmst = 0;
                if (windowToUse == Constants.RECEIVE_WINDOW_1)
                {
                    datr = loraRegion.GetDownstreamDR(request.Rxpk);
                    if (!loraRegion.TryGetUpstreamChannelFrequency(request.Rxpk, out freq) || datr == null)
                    {
                        Logger.Log(loRaDevice.DevEUI, "could not resolve DR and/or frequency for downstream", LogLevel.Error);
                        request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidRxpk);
                        return;
                    }

                    // set tmst for the normal case
                    tmst = request.Rxpk.Tmst + loraRegion.Join_accept_delay1 * 1000000;
                }
                else
                {
                    Logger.Log(devEUI, $"processing of the join request took too long, using second join accept receive window", LogLevel.Debug);
                    tmst = request.Rxpk.Tmst + loraRegion.Join_accept_delay2 * 1000000;

                    (freq, datr) = loraRegion.GetDownstreamRX2DRAndFreq(devEUI, this.configuration.Rx2DataRate, this.configuration.Rx2DataFrequency, null);
                }

                loRaDevice.IsOurDevice = true;
                this.deviceRegistry.UpdateDeviceAfterJoin(loRaDevice, oldDevAddr);

                // Build join accept downlink message
                Array.Reverse(netId);
                Array.Reverse(appNonceBytes);

                // Build the DlSettings fields that is a superposition of RX2DR and RX1DROffset field
                byte[] dlSettings = new byte[1];

                if (request.Region.DRtoConfiguration.ContainsKey(loRaDevice.DesiredRX2DataRate))
                {
                    dlSettings[0] =
                        (byte)(loRaDevice.DesiredRX2DataRate & 0b00001111);
                }
                else
                {
                    Logger.Log(devEUI, $"twin RX2 DR value is not within acceptable values", LogLevel.Error);
                }

                if (request.Region.IsValidRX1DROffset(loRaDevice.DesiredRX1DROffset))
                {
                    var rx1droffset = (byte)(loRaDevice.DesiredRX1DROffset << 4);
                    dlSettings[0] = (byte)(dlSettings[0] + rx1droffset);
                }
                else
                {
                    Logger.Log(devEUI, $"twin RX1 offset DR value is not within acceptable values", LogLevel.Error);
                }

                ushort rxDelay = 0;
                if (request.Region.IsValidRXDelay(loRaDevice.DesiredRXDelay))
                {
                    rxDelay = loRaDevice.DesiredRXDelay;
                }
                else
                {
                    Logger.Log(devEUI, $"twin RX delay value is not within acceptable values", LogLevel.Error);
                }

                var loRaPayloadJoinAccept = new LoRaPayloadJoinAccept(
                    LoRaTools.Utils.ConversionHelper.ByteArrayToString(netId), // NETID 0 / 1 is default test
                    ConversionHelper.StringToByteArray(devAddr), // todo add device address management
                    appNonceBytes,
                    dlSettings,
                    rxDelay,
                    null);

                var joinAccept = loRaPayloadJoinAccept.Serialize(loRaDevice.AppKey, datr, freq, tmst, devEUI);
                if (joinAccept != null)
                {
                    _ = request.PacketForwarder.SendDownstreamAsync(joinAccept);
                    request.NotifySucceeded(loRaDevice, joinAccept);

                    if (Logger.LoggerLevel <= LogLevel.Debug)
                    {
                        var jsonMsg = JsonConvert.SerializeObject(joinAccept);
                        Logger.Log(devEUI, $"{LoRaMessageType.JoinAccept.ToString()} {jsonMsg}", LogLevel.Debug);
                    }
                    else if (Logger.LoggerLevel == LogLevel.Information)
                    {
                        Logger.Log(devEUI, "join accepted", LogLevel.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                var deviceId = devEUI ?? ConversionHelper.ByteArrayToString(request.Payload.DevAddr);
                Logger.Log(deviceId, $"failed to handle join request. {ex.Message}", LogLevel.Error);
                request.NotifyFailed(loRaDevice, ex);
            }
        }
    }
}