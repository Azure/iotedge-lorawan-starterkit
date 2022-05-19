// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using Microsoft.Extensions.Logging;

    public class DefaultClassCDevicesMessageSender : IClassCDeviceMessageSender
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly IDownstreamMessageSender downstreamMessageSender;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private readonly ILogger<DefaultClassCDevicesMessageSender> logger;
        private readonly Counter<int> c2dMessageTooLong;

        public DefaultClassCDevicesMessageSender(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry loRaDeviceRegistry,
            IDownstreamMessageSender downstreamMessageSender,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            ILogger<DefaultClassCDevicesMessageSender> logger,
            Meter meter)
        {
            this.configuration = configuration;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
            this.downstreamMessageSender = downstreamMessageSender;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
            this.logger = logger;
            this.c2dMessageTooLong = meter?.CreateCounter<int>(MetricRegistry.C2DMessageTooLong);
        }

        public async Task<bool> SendAsync(IReceivedLoRaCloudToDeviceMessage message, CancellationToken cts = default)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            var devEui = message.DevEUI.GetValueOrDefault();
            if (!devEui.IsValid)
            {
                this.logger.LogError($"[class-c] devEUI missing/invalid in payload");
                return false;
            }

            if (!message.IsValid(out var validationErrorMessage))
            {
                this.logger.LogError($"[class-c] {validationErrorMessage}");
                return false;
            }

            var loRaDevice = await this.loRaDeviceRegistry.GetDeviceByDevEUIAsync(devEui);
            if (loRaDevice == null)
            {
                this.logger.LogError($"[class-c] device {message.DevEUI} not found or not joined");
                return false;
            }

            if (!RegionManager.TryTranslateToRegion(loRaDevice.LoRaRegion, out var region))
            {
                this.logger.LogError("[class-c] device does not have a region assigned. Ensure the device has connected at least once with the network");
                return false;
            }

            if (cts.IsCancellationRequested)
            {
                this.logger.LogError($"[class-c] device {message.DevEUI} timed out, stopping");
                return false;
            }

            if (loRaDevice.DevAddr is null)
            {
                this.logger.LogError("[class-c] devAddr is empty, cannot send cloud to device message. Ensure the device has connected at least once with the network");
                return false;
            }

            if (loRaDevice.ClassType != LoRaDeviceClassType.C)
            {
                this.logger.LogError($"[class-c] sending cloud to device messages expects a class C device. Class type is {loRaDevice.ClassType}");
                return false;
            }

            if (loRaDevice.LastProcessingStationEui == default)
            {
                this.logger.LogError("[class-c] sending cloud to device messages expects a class C device already connected to one station and reported its StationEui. No StationEui was saved for this device.");
                return false;
            }

            var frameCounterStrategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);
            if (frameCounterStrategy == null)
            {
                this.logger.LogError($"[class-c] could not resolve frame count update strategy for device, gateway id: {loRaDevice.GatewayID}");
                return false;
            }

            var fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, loRaDevice.FCntUp);
            if (fcntDown <= 0)
            {
                this.logger.LogError("[class-c] could not obtain fcnt down for class C device");
                return false;
            }

            this.logger.LogDebug($"[class-c] down frame counter: {loRaDevice.FCntDown}");

            var downlinkMessageBuilderResp = DownlinkMessageBuilder.CreateDownlinkMessage(
                this.configuration,
                loRaDevice, // TODO resolve region from device information
                region,
                message,
                fcntDown,
                this.logger);

            var messageIdLog = message.MessageId ?? "undefined";

            if (downlinkMessageBuilderResp.IsMessageTooLong)
            {
                this.c2dMessageTooLong?.Add(1);
                this.logger.LogError($"[class-c] cloud to device message too large, rejecting. Id: {messageIdLog}");
                if (!await message.RejectAsync())
                {
                    this.logger.LogError($"[class-c] failed to reject. Id: {messageIdLog}");
                }
                return false;
            }
            else
            {
                try
                {
                    await this.downstreamMessageSender.SendDownstreamAsync(downlinkMessageBuilderResp.DownlinkMessage);
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"[class-c] failed to send the message, abandoning. Id: {messageIdLog}, ex: {ex.Message}");
                    if (!await message.AbandonAsync())
                    {
                        this.logger.LogError($"[class-c] failed to abandon the message. Id: {messageIdLog}");
                    }
                    throw;
                }

                if (!await message.CompleteAsync())
                {
                    this.logger.LogError($"[class-c] failed to complete the message. Id: {messageIdLog}");
                }

                if (!await frameCounterStrategy.SaveChangesAsync(loRaDevice))
                {
                    this.logger.LogWarning("[class-c] failed to update framecounter.");
                }
            }

            return true;
        }
    }
}
