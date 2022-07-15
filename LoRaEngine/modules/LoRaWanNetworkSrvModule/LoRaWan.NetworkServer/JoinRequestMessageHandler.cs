// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static LoRaWan.ReceiveWindowNumber;

    public class JoinRequestMessageHandler : IJoinRequestMessageHandler
    {
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly Counter<int> joinRequestCounter;
        private readonly ILogger<JoinRequestMessageHandler> logger;
        private readonly LoRaDeviceAPIServiceBase apiService;
        private readonly Counter<int> receiveWindowHits;
        private readonly Counter<int> receiveWindowMisses;
        private readonly Counter<int> unhandledExceptionCount;
        private readonly NetworkServerConfiguration configuration;
        private readonly IConcentratorDeduplication concentratorDeduplication;

        public JoinRequestMessageHandler(NetworkServerConfiguration configuration,
                                         IConcentratorDeduplication concentratorDeduplication,
                                         ILoRaDeviceRegistry deviceRegistry,
                                         ILogger<JoinRequestMessageHandler> logger,
                                         LoRaDeviceAPIServiceBase apiService,
                                         Meter meter)
        {
            this.configuration = configuration;
            this.concentratorDeduplication = concentratorDeduplication;
            this.deviceRegistry = deviceRegistry;
            this.joinRequestCounter = meter?.CreateCounter<int>(MetricRegistry.JoinRequests);
            this.logger = logger;
            this.apiService = apiService;
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
            var joinReq = (LoRaPayloadJoinRequest)request.Payload;

            var devEui = joinReq.DevEUI;

            using var scope = this.logger.BeginDeviceScope(devEui);

            LoRaDevice loRaDevice = null;

            try
            {
                var timeWatcher = request.GetTimeWatcher();
                var processingTimeout = timeWatcher.GetRemainingTimeToJoinAcceptSecondWindow() - TimeSpan.FromMilliseconds(100);
                using var joinAcceptCancellationToken = new CancellationTokenSource(processingTimeout > TimeSpan.Zero ? processingTimeout : TimeSpan.Zero);

                this.logger.LogInformation("join request received");

                var deduplicationResult = this.concentratorDeduplication.CheckDuplicateJoin(request);
                if (deduplicationResult is ConcentratorDeduplicationResult.NotDuplicate)
                    this.joinRequestCounter?.Add(1);

                if (deduplicationResult is ConcentratorDeduplicationResult.Duplicate)
                {
                    request.NotifyFailed(devEui.ToString(), LoRaDeviceRequestFailedReason.DeduplicationDrop);
                    // we do not log here as the concentratorDeduplication service already does more detailed logging
                    return;
                }

                loRaDevice = await this.deviceRegistry.GetDeviceForJoinRequestAsync(devEui, joinReq.DevNonce);
                if (loRaDevice == null)
                {
                    request.NotifyFailed(devEui.ToString(), LoRaDeviceRequestFailedReason.UnknownDevice);
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

                if (loRaDevice.AppEui != joinReq.AppEui)
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

                var netId = this.configuration.NetId;
                var appNonce = new AppNonce(RandomNumberGenerator.GetInt32(toExclusive: AppNonce.MaxValue + 1));
                var appSKey = OTAAKeysGenerator.CalculateAppSessionKey(appNonce, netId, joinReq.DevNonce, appKey);
                var nwkSKey = OTAAKeysGenerator.CalculateNetworkSessionKey(appNonce, netId, joinReq.DevNonce, appKey);
                var address = RandomNumberGenerator.GetInt32(toExclusive: DevAddr.MaxNetworkAddress + 1);
                // The 7 LBS of the NetID become the NwkID of a DevAddr:
                var devAddr = new DevAddr(unchecked((byte)netId.NetworkId), address);

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
                    NetId = netId,
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

                var deviceUpdateSucceeded = await loRaDevice.UpdateAfterJoinAsync(updatedProperties, joinAcceptCancellationToken.Token);

                if (!deviceUpdateSucceeded)
                {
                    this.logger.LogError("join refused: join request could not save twin");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.IoTHubProblem);
                    return;
                }

                this.deviceRegistry.UpdateDeviceAfterJoin(loRaDevice, oldDevAddr);

                var windowToUse = timeWatcher.ResolveJoinAcceptWindowToUse();
                if (windowToUse is null)
                {
                    this.receiveWindowMisses?.Add(1);
                    this.logger.LogInformation("join refused: processing of the join request took too long, sending no message");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ReceiveWindowMissed);
                    return;
                }

                // Build join accept downlink message

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
                var loraSpecDesiredRxDelay = RxDelay.RxDelay0;
                if (Enum.IsDefined(loRaDevice.DesiredRXDelay))
                {
                    loraSpecDesiredRxDelay = loRaDevice.DesiredRXDelay;
                }
                else
                {
                    this.logger.LogError("twin RX delay value is not within acceptable values");
                }

                var loRaPayloadJoinAccept = new LoRaPayloadJoinAccept(
                    netId, // NETID 0 / 1 is default test
                    devAddr, // todo add device address management
                    appNonce,
                    dlSettings,
                    loraSpecDesiredRxDelay,
                    null);

                var loraRegion = request.Region;
                if (!loraRegion.TryGetDownstreamChannelFrequency(request.RadioMetadata.Frequency, upstreamDataRate: request.RadioMetadata.DataRate, deviceJoinInfo: deviceJoinInfo, downstreamFrequency: out var freq))
                {
                    this.logger.LogError("could not resolve DR and/or frequency for downstream");
                    request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidUpstreamMessage);
                    return;
                }

                var joinAcceptBytes = loRaPayloadJoinAccept.Serialize(appKey);

                // For join accept messages the RX1DROffsetvalue is ignored as the join accept message carry the settings to the device.
                var rx1 = windowToUse is not ReceiveWindow2
                        ? new ReceiveWindow(loraRegion.GetDownstreamDataRate(request.RadioMetadata.DataRate), freq)
                        : (ReceiveWindow?)null;

                var rx2 = new ReceiveWindow(loraRegion.GetDownstreamRX2DataRate(this.configuration.Rx2DataRate, null, deviceJoinInfo, this.logger),
                                            loraRegion.GetDownstreamRX2Freq(this.configuration.Rx2Frequency, deviceJoinInfo, this.logger));

                var downlinkMessage = new DownlinkMessage(joinAcceptBytes,
                                                          request.RadioMetadata.UpInfo.Xtime,
                                                          rx1,
                                                          rx2,
                                                          loRaDevice.DevEUI,
                                                          loraRegion.JoinAcceptDelay1,
                                                          loRaDevice.ClassType,
                                                          request.StationEui,
                                                          request.RadioMetadata.UpInfo.AntennaPreference);

                this.receiveWindowHits?.Add(1, KeyValuePair.Create(MetricRegistry.ReceiveWindowTagName, (object)windowToUse));
                _ = request.DownstreamMessageSender.SendDownstreamAsync(downlinkMessage);
                _ = this.apiService.SendJoinNotificationAsync(new DeviceJoinNotification
                {
                    DevAddr = devAddr,
                    DevEUI = devEui,
                    GatewayId = loRaDevice.GatewayID,
                    NwkSKey = nwkSKey
                }, joinAcceptCancellationToken.Token);

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
            finally
            {
                if (loRaDevice is { } someLoRaDevice)
                    await someLoRaDevice.CloseConnectionAsync(CancellationToken.None, true);
            }
        }
    }
}
