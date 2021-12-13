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
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class JoinRequestMessageHandler : IJoinRequestMessageHandler
    {
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly ILogger<JoinRequestMessageHandler> logger;
        private readonly Counter<int> receiveWindowHits;
        private readonly Counter<int> receiveWindowMisses;
        private readonly Counter<int> unhandledExceptionCount;
        private readonly Counter<int> deviceLoadRequests;
        private readonly NetworkServerConfiguration configuration;

        public JoinRequestMessageHandler(NetworkServerConfiguration configuration,
                                         ILoRaDeviceRegistry deviceRegistry,
                                         ILogger<JoinRequestMessageHandler> logger,
                                         Meter meter)
        {
            this.configuration = configuration;
            this.deviceRegistry = deviceRegistry;
            this.logger = logger;
            this.receiveWindowHits = meter?.CreateCounter<int>(MetricRegistry.ReceiveWindowHits);
            this.receiveWindowMisses = meter?.CreateCounter<int>(MetricRegistry.ReceiveWindowMisses);
            this.unhandledExceptionCount = meter?.CreateCounter<int>(MetricRegistry.UnhandledExceptions);
            this.deviceLoadRequests = meter?.CreateCounter<int>(MetricRegistry.DeviceLoadRequests);
        }

        public void DispatchRequest(LoRaRequest request)
        {
            // Unobserved task exceptions are logged as part of ProcessJoinRequestAsync.
            _ = Task.Run(() => ProcessJoinRequestAsync(request));

            async Task ProcessJoinRequestAsync(LoRaRequest request)
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

                    var devNonce = joinReq.GetDevNonceAsString();
                    this.logger.LogInformation("join request received");

                    loRaDevice = await this.deviceRegistry.GetDeviceForJoinRequestAsync(devEUI, devNonce);
                    this.deviceLoadRequests?.Add(1);
                    if (loRaDevice == null)
                    {
                        request.NotifyFailed(devEUI, LoRaDeviceRequestFailedReason.UnknownDevice);
                        // we do not log here as we assume that the deviceRegistry does a more informed logging if returning null
                        return;
                    }

                    if (string.IsNullOrEmpty(loRaDevice.AppKey))
                    {
                        this.logger.LogError("join refused: missing AppKey for OTAA device");
                        request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidJoinRequest);
                        return;
                    }

                    if (loRaDevice.AppEUI != appEUI)
                    {
                        this.logger.LogError("join refused: AppEUI for OTAA does not match device");
                        request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidJoinRequest);
                        return;
                    }

                    if (!joinReq.CheckMic(loRaDevice.AppKey))
                    {
                        this.logger.LogError("join refused: invalid MIC");
                        request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.JoinMicCheckFailed);
                        return;
                    }

                    // Make sure that is a new request and not a replay
                    if (!string.IsNullOrEmpty(loRaDevice.DevNonce) && loRaDevice.DevNonce == devNonce)
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
                    var appKeyBytes = ConversionHelper.StringToByteArray(loRaDevice.AppKey);
                    var appSKey = OTAAKeysGenerator.CalculateKey(new byte[1] { 0x02 }, appNonceBytes, netId, joinReq.DevNonce, appKeyBytes);
                    var nwkSKey = OTAAKeysGenerator.CalculateKey(new byte[1] { 0x01 }, appNonceBytes, netId, joinReq.DevNonce, appKeyBytes);
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
                        DevNonce = devNonce,
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

                    if (request.Region.LoRaRegion == LoRaRegionType.CN470RP2)
                    {
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                        if (request.Region.TryGetJoinChannelIndex(request.Rxpk, out var channelIndex))
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                        {
                            updatedProperties.CN470JoinChannel = channelIndex;
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

                    Hertz freq;
                    string datr = null;
                    uint tmst = 0;
                    ushort lnsRxDelay = 0;
                    if (windowToUse == Constants.ReceiveWindow1)
                    {
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                        datr = loraRegion.GetDownstreamDataRate(request.Rxpk);
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                        if (!loraRegion.TryGetDownstreamChannelFrequency(request.Rxpk.FreqHertz, out freq) || datr == null)
                        {
                            this.logger.LogError("could not resolve DR and/or frequency for downstream");
                            request.NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.InvalidRxpk);
                            return;
                        }

                        // set tmst for the normal case
                        tmst = request.Rxpk.Tmst + (loraRegion.JoinAcceptDelay1 * 1000000);
                        lnsRxDelay = (ushort)loraRegion.JoinAcceptDelay1;
                    }
                    else
                    {
                        this.logger.LogDebug("processing of the join request took too long, using second join accept receive window");
                        tmst = request.Rxpk.Tmst + (loraRegion.JoinAcceptDelay2 * 1000000);
                        lnsRxDelay = (ushort)loraRegion.JoinAcceptDelay2;

                        freq = loraRegion.GetDownstreamRX2Freq(this.configuration.Rx2Frequency, logger);
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                        datr = loraRegion.GetDownstreamRX2DataRate(devEUI, this.configuration.Rx2DataRate, null, logger);
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
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
                            dlSettings[0] =
                                (byte)(loRaDevice.DesiredRX2DataRate & 0b00001111);
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

                    var joinAccept = loRaPayloadJoinAccept.Serialize(loRaDevice.AppKey, datr, freq, devEUI, tmst, lnsRxDelay, request.Rxpk.Rfch, request.Rxpk.Time, request.StationEui);
                    if (joinAccept != null)
                    {
                        this.receiveWindowHits?.Add(1, KeyValuePair.Create(MetricRegistry.ReceiveWindowTagName, (object)windowToUse));
                        _ = request.PacketForwarder.SendDownstreamAsync(joinAccept);
                        request.NotifySucceeded(loRaDevice, joinAccept);

                        if (this.logger.IsEnabled(LogLevel.Debug))
                        {
                            var jsonMsg = JsonConvert.SerializeObject(joinAccept);
                            this.logger.LogDebug($"{LoRaMessageType.JoinAccept} {jsonMsg}");
                        }
                        else
                        {
                            this.logger.LogInformation("join accepted");
                        }
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
}
