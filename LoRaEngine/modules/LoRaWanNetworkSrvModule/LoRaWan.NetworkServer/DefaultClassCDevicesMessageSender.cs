// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using Microsoft.Extensions.Logging;

    public class DefaultClassCDevicesMessageSender : IClassCDeviceMessageSender
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly IPacketForwarder packetForwarder;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private readonly ILogger<DefaultClassCDevicesMessageSender> logger;

        public DefaultClassCDevicesMessageSender(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry loRaDeviceRegistry,
            IPacketForwarder packetForwarder,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            ILogger<DefaultClassCDevicesMessageSender> logger)
        {
            this.configuration = configuration;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
            this.packetForwarder = packetForwarder;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
            this.logger = logger;
        }

        public async Task<bool> SendAsync(IReceivedLoRaCloudToDeviceMessage message, CancellationToken cts = default)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrEmpty(message.DevEUI))
            {
                this.logger.LogError($"[class-c] devEUI missing in payload");
                return false;
            }

            if (!message.IsValid(out var validationErrorMessage))
            {
                this.logger.LogError($"[class-c] {validationErrorMessage}");
                return false;
            }

            var loRaDevice = await this.loRaDeviceRegistry.GetDeviceByDevEUIAsync(message.DevEUI);
            if (loRaDevice == null)
            {
                this.logger.LogError($"[class-c] device {message.DevEUI} not found");
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

            if (string.IsNullOrEmpty(loRaDevice.DevAddr))
            {
                this.logger.LogError("[class-c] devAddr is empty, cannot send cloud to device message. Ensure the device has connected at least once with the network");
                return false;
            }

            if (loRaDevice.ClassType != LoRaDeviceClassType.C)
            {
                this.logger.LogError(loRaDevice.DevEUI, $"[class-c] sending cloud to device messages expects a class C device. Class type is {loRaDevice.ClassType}");
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

            var fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, 0);
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

            if (downlinkMessageBuilderResp.IsMessageTooLong)
            {
                this.logger.LogError($"[class-c] cloud to device message too large, rejecting. Id: {message.MessageId ?? "undefined"}");
                if (!await message.RejectAsync())
                {
                    this.logger.LogError($"[class-c] failed to reject. Id: {message.MessageId ?? "undefined"}");
                }
                return false;
            }
            else
            {
                await this.packetForwarder.SendDownstreamAsync(downlinkMessageBuilderResp.DownlinkPktFwdMessage);
                if (!await frameCounterStrategy.SaveChangesAsync(loRaDevice))
                {
                    this.logger.LogWarning("[class-c] failed to update framecounter.");
                }
            }

            return true;
        }
    }
}
