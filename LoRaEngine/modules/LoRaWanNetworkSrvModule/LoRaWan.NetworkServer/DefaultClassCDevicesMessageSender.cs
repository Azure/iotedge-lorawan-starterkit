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

        public async Task<bool> SendAsync(IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage, CancellationToken cts = default(CancellationToken))
        {
            try
            {
                if (string.IsNullOrEmpty(cloudToDeviceMessage.DevEUI))
                {
                    Logger.Log($"[class-c] devEUI missing in payload", LogLevel.Error);
                    return false;
                }

                if (!cloudToDeviceMessage.IsValid(out var validationErrorMessage))
                {
                    Logger.Log(cloudToDeviceMessage.DevEUI, $"[class-c] {validationErrorMessage}", LogLevel.Error);
                    return false;
                }

                var loRaDevice = await this.loRaDeviceRegistry.GetDeviceByDevEUIAsync(cloudToDeviceMessage.DevEUI);
                if (loRaDevice == null)
                {
                    Logger.Log(cloudToDeviceMessage.DevEUI, $"[class-c] device {cloudToDeviceMessage.DevEUI} not found", LogLevel.Error);
                    return false;
                }

                if (!RegionManager.TryTranslateToRegion(loRaDevice.LoRaRegion, out var region))
                {
                    Logger.Log(cloudToDeviceMessage.DevEUI, $"[class-c] device does not have a region assigned. Ensure the device has connected at least once with the network", LogLevel.Error);
                    return false;
                }

                if (cts.IsCancellationRequested)
                {
                    Logger.Log(cloudToDeviceMessage.DevEUI, $"[class-c] device {cloudToDeviceMessage.DevEUI} timed out, stopping", LogLevel.Error);
                    return false;
                }

                if (string.IsNullOrEmpty(loRaDevice.DevAddr))
                {
                    Logger.Log(loRaDevice.DevEUI, "[class-c] devAddr is empty, cannot send cloud to device message. Ensure the device has connected at least once with the network", LogLevel.Error);
                    return false;
                }

                if (loRaDevice.ClassType != LoRaDeviceClassType.C)
                {
                    Logger.Log(loRaDevice.DevEUI, $"[class-c] sending cloud to device messages expects a class C device. Class type is {loRaDevice.ClassType}", LogLevel.Error);
                    return false;
                }

                var frameCounterStrategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);
                if (frameCounterStrategy == null)
                {
                    Logger.Log(loRaDevice.DevEUI, $"[class-c] could not resolve frame count update strategy for device, gateway id: {loRaDevice.GatewayID}", LogLevel.Error);
                    return false;
                }

                var fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, 0);
                if (fcntDown <= 0)
                {
                    Logger.Log(loRaDevice.DevEUI, "[class-c] could not obtain fcnt down for class C device", LogLevel.Error);
                    return false;
                }

                Logger.Log(loRaDevice.DevEUI, $"[class-c] down frame counter: {loRaDevice.FCntDown}", LogLevel.Debug);

                var downlinkMessageBuilderResp = DownlinkMessageBuilder.CreateDownlinkMessage(
                    this.configuration,
                    loRaDevice, // TODO resolve region from device information
                    region,
                    cloudToDeviceMessage,
                    fcntDown);

                if (downlinkMessageBuilderResp.IsMessageTooLong)
                {
                    Logger.Log(loRaDevice.DevEUI, $"[class-c] cloud to device message too large, rejecting. Id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Error);
                    await cloudToDeviceMessage.RejectAsync();
                    return false;
                }
                else
                {
                    await this.packetForwarder.SendDownstreamAsync(downlinkMessageBuilderResp.DownlinkPktFwdMessage);
                    await frameCounterStrategy.SaveChangesAsync(loRaDevice);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(cloudToDeviceMessage.DevEUI, $"[class-c] error sending class C cloud to device message. {ex.Message}", LogLevel.Error);
                return false;
            }
        }
    }
}
