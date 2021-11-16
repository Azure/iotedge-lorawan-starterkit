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

        public DefaultClassCDevicesMessageSender(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry loRaDeviceRegistry,
            IPacketForwarder packetForwarder,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider)
        {
            this.configuration = configuration;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
            this.packetForwarder = packetForwarder;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
        }

        public async Task<bool> SendAsync(IReceivedLoRaCloudToDeviceMessage message, CancellationToken cts = default)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));

            if (string.IsNullOrEmpty(message.DevEUI))
            {
                TcpLogger.Log($"[class-c] devEUI missing in payload", LogLevel.Error);
                return false;
            }

            if (!message.IsValid(out var validationErrorMessage))
            {
                TcpLogger.Log(message.DevEUI, $"[class-c] {validationErrorMessage}", LogLevel.Error);
                return false;
            }

            var loRaDevice = await this.loRaDeviceRegistry.GetDeviceByDevEUIAsync(message.DevEUI);
            if (loRaDevice == null)
            {
                TcpLogger.Log(message.DevEUI, $"[class-c] device {message.DevEUI} not found", LogLevel.Error);
                return false;
            }

            if (!RegionManager.TryTranslateToRegion(loRaDevice.LoRaRegion, out var region))
            {
                TcpLogger.Log(message.DevEUI, $"[class-c] device does not have a region assigned. Ensure the device has connected at least once with the network", LogLevel.Error);
                return false;
            }

            if (cts.IsCancellationRequested)
            {
                TcpLogger.Log(message.DevEUI, $"[class-c] device {message.DevEUI} timed out, stopping", LogLevel.Error);
                return false;
            }

            if (string.IsNullOrEmpty(loRaDevice.DevAddr))
            {
                TcpLogger.Log(loRaDevice.DevEUI, "[class-c] devAddr is empty, cannot send cloud to device message. Ensure the device has connected at least once with the network", LogLevel.Error);
                return false;
            }

            if (loRaDevice.ClassType != LoRaDeviceClassType.C)
            {
                TcpLogger.Log(loRaDevice.DevEUI, $"[class-c] sending cloud to device messages expects a class C device. Class type is {loRaDevice.ClassType}", LogLevel.Error);
                return false;
            }

            if (loRaDevice.LastProcessingStationEui == default)
            {
                TcpLogger.Log(loRaDevice.DevEUI, $"[class-c] sending cloud to device messages expects a class C device already connected to one station and reported its StationEui. No StationEui was saved for this device.", LogLevel.Error);
                return false;
            }

            var frameCounterStrategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);
            if (frameCounterStrategy == null)
            {
                TcpLogger.Log(loRaDevice.DevEUI, $"[class-c] could not resolve frame count update strategy for device, gateway id: {loRaDevice.GatewayID}", LogLevel.Error);
                return false;
            }

            var fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, 0);
            if (fcntDown <= 0)
            {
                TcpLogger.Log(loRaDevice.DevEUI, "[class-c] could not obtain fcnt down for class C device", LogLevel.Error);
                return false;
            }

            TcpLogger.Log(loRaDevice.DevEUI, $"[class-c] down frame counter: {loRaDevice.FCntDown}", LogLevel.Debug);

            var downlinkMessageBuilderResp = DownlinkMessageBuilder.CreateDownlinkMessage(
                this.configuration,
                loRaDevice, // TODO resolve region from device information
                region,
                message,
                fcntDown);

            if (downlinkMessageBuilderResp.IsMessageTooLong)
            {
                TcpLogger.Log(loRaDevice.DevEUI, $"[class-c] cloud to device message too large, rejecting. Id: {message.MessageId ?? "undefined"}", LogLevel.Error);
                if (!await message.RejectAsync())
                {
                    TcpLogger.Log(loRaDevice.DevEUI, $"[class-c] failed to reject. Id: {message.MessageId ?? "undefined"}", LogLevel.Error);
                }
                return false;
            }
            else
            {
                await this.packetForwarder.SendDownstreamAsync(downlinkMessageBuilderResp.DownlinkPktFwdMessage);
                if (!await frameCounterStrategy.SaveChangesAsync(loRaDevice))
                {
                    TcpLogger.Log(loRaDevice.DevEUI, $"[class-c] failed to update framecounter.", LogLevel.Warning);
                }
            }

            return true;
        }
    }
}
