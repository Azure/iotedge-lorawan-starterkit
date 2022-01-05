// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class JoinRequestMessageHandler : IJoinRequestMessageHandler
    {
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly Counter<int> joinRequestCounter;
        private readonly ILogger<JoinRequestMessageHandler> logger;
        private readonly Counter<int> receiveWindowHits;
        private readonly Counter<int> receiveWindowMisses;
        private readonly Counter<int> unhandledExceptionCount;
        private readonly NetworkServerConfiguration configuration;
        private readonly IConcentratorDeduplication concentratorDeduplication;

        public JoinRequestMessageHandler(NetworkServerConfiguration configuration,
                                         IConcentratorDeduplication concentratorDeduplication,
                                         ILoRaDeviceRegistry deviceRegistry,
                                         ILogger<JoinRequestMessageHandler> logger,
                                         Meter meter)
        {
            this.configuration = configuration;
            this.concentratorDeduplication = concentratorDeduplication;
            this.deviceRegistry = deviceRegistry;
            this.joinRequestCounter = meter?.CreateCounter<int>(MetricRegistry.JoinRequests);
            this.logger = logger;
            this.receiveWindowHits = meter?.CreateCounter<int>(MetricRegistry.ReceiveWindowHits);
            this.receiveWindowMisses = meter?.CreateCounter<int>(MetricRegistry.ReceiveWindowMisses);
            this.unhandledExceptionCount = meter?.CreateCounter<int>(MetricRegistry.UnhandledExceptions);
        }

        public void DispatchRequest(LoRaRequest request)
        {
            // Unobserved task exceptions are logged as part of ProcessJoinRequestAsync.
            _ = Task.Run(() => ProcessJoinRequestAsync(request));
        }

        internal async Task ProcessJoinRequestAsync(LoRaRequest request)
        {
            LoRaDevice loRaDevice = null;
            string devEUI = null;
            var loraRegion = request.Region;

            try
            {
                var timeWatcher = request.GetTimeWatcher();

                var joinReq = (LoRaPayloadJoinRequest)request.Payload;

                devEUI = joinReq.GetDevEUIAsString();
                var appEUI = joinReq.GetAppEUIAsString();

                using var scope = this.logger.BeginDeviceScope(devEUI);

                this.logger.LogInformation("join request received");

                if (this.concentratorDeduplication.CheckDuplicateJoin(request) is ConcentratorDeduplicationResult.Duplicate)
                {
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.DeduplicationDrop);
                    // we do not log here as the concentratorDeduplication service already does more detailed logging
                    return;
                }

                loRaDevice = await this.deviceRegistry.GetDeviceForJoinRequestAsync(devEUI, joinReq.DevNonce);
                if (loRaDevice == null)
                {
                    request.NotifyFailed(devEUI, LoRaDeviceRequestFailedReason.UnknownDevice);
                    // we do not log here as we assume that the deviceRegistry does a more informed logging if returning null
                    return;
                }

                if (loRaDevice.AppKey is null)
                {
                    this.logger.LogError("join refused: missing AppKey for OTAA device");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidJoinRequest);
                    return;
                }

                var appKey = loRaDevice.AppKey.Value;

                this.joinRequestCounter?.Add(1);

                if (loRaDevice.AppEUI != appEUI)
                {
                    this.logger.LogError("join refused: AppEUI for OTAA does not match device");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidJoinRequest);
                    return;
                }

                if (!joinReq.CheckMic(appKey))
                {
                    this.logger.LogError("join refused: invalid MIC");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.JoinMicCheckFailed);
                    return;
                }

                // Make sure that is a new request and not a replay
                if (loRaDevice.DevNonce is { } devNonce && devNonce == joinReq.DevNonce)
                {
                    if (string.IsNullOrEmpty(loRaDevice.GatewayID))
                    {
                        this.logger.LogInformation("join refused: join already processed by another gateway");
                    }
                    else
                    {
                        this.logger.LogError("join refused: DevNonce already used by this device");
                    }

                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.JoinDevNonceAlreadyUsed);
                    return;
                }

                // Check that the device is joining through the linked gateway and not another
                if (!loRaDevice.IsOurDevice)
                {
                    this.logger.LogInformation("join refused: trying to join not through its linked gateway, ignoring join request");
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
                var appNonceBytes = ConversionHelper.StringToByteArray(appNonce);
                var appSKey = OTAAKeysGenerator.CalculateAppSessionKey(new byte[1] { 0x02 }, appNonceBytes, netId, joinReq.DevNonce, appKey);
                var nwkSKey = OTAAKeysGenerator.CalculateNetworkSessionKey(new byte[1] { 0x01 }, appNonceBytes, netId, joinReq.DevNonce, appKey);
                var devAddr = OTAAKeysGenerator.GetNwkId(netId);

                var oldDevAddr = loRaDevice.DevAddr;

                if (!timeWatcher.InTimeForJoinAccept())
                {
                    this.receiveWindowMisses?.Add(1);
                    // in this case it's too late, we need to break and avoid saving twins
                    this.logger.LogInformation("join refused: processing of the join request took too long, sending no message");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                var updatedProperties = new LoRaDeviceJoinUpdateProperties
                {
                    DevAddr = devAddr,
                    NwkSKey = nwkSKey,
                    AppSKey = appSKey,
                    AppNonce = appNonce,
                    DevNonce = joinReq.DevNonce,
                    NetID = ConversionHelper.ByteArrayToString(netId),
                    Region = request.Region.LoRaRegion,
                    PreferredGatewayID = this.configuration.GatewayID,
                };

                if (loRaDevice.ClassType == LoRaDeviceClassType.C)
                {
                    updatedProperties.SavePreferredGateway = true;
                    updatedProperties.SaveRegion = true;
                    updatedProperties.StationEui = request.StationEui;
                }

                DeviceJoinInfo deviceJoinInfo = null;
                if (request.Region.LoRaRegion == LoRaRegionType.CN470RP2)
                {
                    if (request.Region.TryGetJoinChannelIndex(request.RadioMetadata.Frequency, out var channelIndex))
                    {
                        updatedProperties.CN470JoinChannel = channelIndex;
                        deviceJoinInfo = new DeviceJoinInfo(channelIndex);
                    }
                    else
                    {
                        this.logger.LogError("failed to retrieve the join channel index for device");
                    }
                }

                var deviceUpdateSucceeded = await loRaDevice.UpdateAfterJoinAsync(updatedProperties);

                if (!deviceUpdateSucceeded)
                {
                    this.logger.LogError("join refused: join request could not save twin");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ApplicationError);
                    return;
                }

                var windowToUse = timeWatcher.ResolveJoinAcceptWindowToUse();
                if (windowToUse == Constants.InvalidReceiveWindow)
                {
                    this.receiveWindowMisses?.Add(1);
                    this.logger.LogInformation("join refused: processing of the join request took too long, sending no message");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                ushort lnsRxDelay = 0;
                if (windowToUse == Constants.ReceiveWindow1)
                {
                    // set tmst for the normal case
                    lnsRxDelay = (ushort)loraRegion.JoinAcceptDelay1;
                }
                else
                {
                    this.logger.LogDebug("processing of the join request took too long, using second join accept receive window");
                    lnsRxDelay = (ushort)loraRegion.JoinAcceptDelay2;
                }

                this.deviceRegistry.UpdateDeviceAfterJoin(loRaDevice, oldDevAddr);

                // Build join accept downlink message
                Array.Reverse(netId);
                Array.Reverse(appNonceBytes);

                // Build the DlSettings fields that is a superposition of RX2DR and RX1DROffset field
                var dlSettings = new byte[1];

                if (loRaDevice.DesiredRX2DataRate.HasValue)
                {
                    if (request.Region.DRtoConfiguration.ContainsKey(loRaDevice.DesiredRX2DataRate.Value))
                    {
                        dlSettings[0] = (byte)((byte)loRaDevice.DesiredRX2DataRate & 0b00001111);
                    }
                    else
                    {
                        this.logger.LogError("twin RX2 DR value is not within acceptable values");
                    }
                }

                if (request.Region.IsValidRX1DROffset(loRaDevice.DesiredRX1DROffset))
                {
                    var rx1droffset = (byte)(loRaDevice.DesiredRX1DROffset << 4);
                    dlSettings[0] = (byte)(dlSettings[0] + rx1droffset);
                }
                else
                {
                    this.logger.LogError("twin RX1 offset DR value is not within acceptable values");
                }

                // The following DesiredRxDelay is different than the RxDelay to be passed to Serialize function
                // This one is a delay between TX and RX for any message to be processed by joining device
                // The field accepted by Serialize method is an indication of the delay (compared to receive time of join request)
                // of when the message Join Accept message should be sent
                ushort loraSpecDesiredRxDelay = 0;
                if (Region.IsValidRXDelay(loRaDevice.DesiredRXDelay))
                {
                    loraSpecDesiredRxDelay = loRaDevice.DesiredRXDelay;
                }
                else
                {
                    this.logger.LogError("twin RX delay value is not within acceptable values");
                }

                var loRaPayloadJoinAccept = new LoRaPayloadJoinAccept(
                    ConversionHelper.ByteArrayToString(netId), // NETID 0 / 1 is default test
                    ConversionHelper.StringToByteArray(devAddr), // todo add device address management
                    appNonceBytes,
                    dlSettings,
                    loraSpecDesiredRxDelay,
                    null);

                if (!loraRegion.TryGetDownstreamChannelFrequency(request.RadioMetadata.Frequency, out var freq, deviceJoinInfo: deviceJoinInfo))
                {
                    this.logger.LogError("could not resolve DR and/or frequency for downstream");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidUpstreamMessage);
                    return;
                }

                var joinAcceptBytes = loRaPayloadJoinAccept.Serialize(appKey);
                var downlinkMessage = new DownlinkMessage(
                  joinAcceptBytes,
                  request.RadioMetadata.UpInfo.Xtime,
                  loraRegion.GetDownstreamDataRate(request.RadioMetadata.DataRate, loRaDevice.ReportedRX1DROffset),
                  loraRegion.GetDownstreamRX2DataRate(this.configuration.Rx2DataRate, null, logger),
                  freq,
                  loraRegion.GetDownstreamRX2Freq(this.configuration.Rx2Frequency, logger),
                  DevEui.Parse(loRaDevice.DevEUI),
                  lnsRxDelay,
                  request.StationEui,
                  request.RadioMetadata.UpInfo.AntennaPreference
                  );


                this.receiveWindowHits?.Add(1, KeyValuePair.Create(MetricRegistry.ReceiveWindowTagName, (object)windowToUse));
                _ = request.PacketForwarder.SendDownstreamAsync(downlinkMessage);
                request.NotifySucceeded(loRaDevice, downlinkMessage);

                if (this.logger.IsEnabled(LogLevel.Debug))
                {
                    var jsonMsg = JsonConvert.SerializeObject(downlinkMessage);
                    this.logger.LogDebug($"{MacMessageType.JoinAccept} {jsonMsg}");
                }
                else
                {
                    this.logger.LogInformation("join accepted");
                }

            }
            catch (Exception ex) when
                (ExceptionFilterUtility.True(() => this.logger.LogError(ex, $"failed to handle join request. {ex.Message}", LogLevel.Error),
                                                () => this.unhandledExceptionCount?.Add(1)))
            {
                request.NotifyFailed(loRaDevice, ex);
                throw;
            }
        }
    }
}
