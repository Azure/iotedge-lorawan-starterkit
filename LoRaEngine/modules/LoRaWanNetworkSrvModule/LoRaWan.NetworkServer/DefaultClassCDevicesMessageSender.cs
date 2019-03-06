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
        private readonly Region loRaRegion;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly IPacketForwarder packetForwarder;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;

        public DefaultClassCDevicesMessageSender(
            NetworkServerConfiguration configuration,
            Region loRaRegion,
            ILoRaDeviceRegistry loRaDeviceRegistry,
            IPacketForwarder packetForwarder,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider)
        {
            this.configuration = configuration;
            this.loRaRegion = loRaRegion;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
            this.packetForwarder = packetForwarder;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
        }

        public async Task<bool> SendAsync(ILoRaCloudToDeviceMessage cloudToDeviceMessage, CancellationToken cts = default(CancellationToken))
        {
            try
            {
                if (string.IsNullOrEmpty(cloudToDeviceMessage.DevEUI))
                {
                    Logger.Log($"[C2D] DevEUI missing in payload", LogLevel.Error);
                    return false;
                }

                var loRaDevice = await this.loRaDeviceRegistry.GetDeviceByDevEUIAsync(cloudToDeviceMessage.DevEUI);
                if (loRaDevice == null)
                {
                    Logger.Log(cloudToDeviceMessage.DevEUI, $"[C2D] Device {cloudToDeviceMessage.DevEUI} not found", LogLevel.Error);
                    return false;
                }

                if (cts.IsCancellationRequested)
                {
                    Logger.Log(cloudToDeviceMessage.DevEUI, $"[C2D] Device {cloudToDeviceMessage.DevEUI} timed out, stopping", LogLevel.Error);
                    return false;
                }

                if (string.IsNullOrEmpty(loRaDevice.DevAddr))
                {
                    Logger.Log(loRaDevice.DevEUI, "Device devAddr is empty, cannot send cloud to device message", LogLevel.Information);
                    return false;
                }

                if (loRaDevice.ClassType != LoRaDeviceClassType.C)
                {
                    Logger.Log(loRaDevice.DevEUI, $"Sending cloud to device messages expects a class C device. Class type is {loRaDevice.ClassType}", LogLevel.Information);
                    return false;
                }

                var frameCounterStrategy = this.frameCounterUpdateStrategyProvider.GetStrategy(loRaDevice.GatewayID);
                if (frameCounterStrategy == null)
                {
                    Logger.Log(loRaDevice.DevEUI, $"Could not resolve frame count update strategy for device, gateway id: {loRaDevice.GatewayID}", LogLevel.Information);
                    return false;
                }

                var fcntDown = await frameCounterStrategy.NextFcntDown(loRaDevice, 0);
                if (fcntDown <= 0)
                {
                    Logger.Log(loRaDevice.DevEUI, "Could not obtain fcnt down for class C device", LogLevel.Information);
                    return false;
                }

                var downlinkMessageBuilderResp = DownlinkMessageBuilder.CreateDownlinkMessage(
                    this.configuration,
                    loRaDevice, // TODO resolve region from device information
                    this.loRaRegion ?? RegionFactory.CurrentRegion ?? RegionFactory.CreateEU868Region(),
                    cloudToDeviceMessage,
                    fcntDown);

                if (downlinkMessageBuilderResp.IsMessageTooLong)
                {
                    Logger.Log(loRaDevice.DevEUI, $"class C cloud to device message too large, rejecting. Id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Information);
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
                Logger.Log(cloudToDeviceMessage.DevEUI, $"Error sending class C cloud to device message. {ex.Message}", LogLevel.Error);
                return false;
            }
        }
    }
}
