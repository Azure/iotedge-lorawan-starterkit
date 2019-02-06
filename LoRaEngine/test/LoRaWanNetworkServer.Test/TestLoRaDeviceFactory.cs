// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using LoRaWan.NetworkServer;

    internal class TestLoRaDeviceFactory : ILoRaDeviceFactory
    {
        private readonly ILoRaDeviceClient loRaDeviceClient;
        private readonly ILoRaDataRequestHandler requestHandler;
        private readonly Dictionary<string, ILoRaDeviceClient> deviceClientMap;
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory;

        public TestLoRaDeviceFactory(ILoRaDeviceClient loRaDeviceClient)
        {
            this.loRaDeviceClient = loRaDeviceClient;
            this.deviceClientMap = new Dictionary<string, ILoRaDeviceClient>();
        }

        public TestLoRaDeviceFactory(ILoRaDeviceClient loRaDeviceClient, ILoRaDataRequestHandler requestHandler)
        {
            this.loRaDeviceClient = loRaDeviceClient;
            this.requestHandler = requestHandler;
            this.deviceClientMap = new Dictionary<string, ILoRaDeviceClient>();
        }

        public TestLoRaDeviceFactory(
            NetworkServerConfiguration configuration,
            ILoRaDeviceFrameCounterUpdateStrategyFactory frameCounterUpdateStrategyFactory,
            ILoRaDeviceClient loRaDeviceClient)
            : this(loRaDeviceClient)
        {
            this.configuration = configuration;
            this.frameCounterUpdateStrategyFactory = frameCounterUpdateStrategyFactory;
        }

        public LoRaDevice Create(IoTHubDeviceInfo deviceInfo)
        {
            if (!this.deviceClientMap.TryGetValue(deviceInfo.DevEUI, out var deviceClientToAssign))
            {
                deviceClientToAssign = this.loRaDeviceClient;
            }

            var loRaDevice = new LoRaDevice(
                deviceInfo.DevAddr,
                deviceInfo.DevEUI,
                deviceClientToAssign);

            loRaDevice.SetRequestHandler(this.requestHandler ?? new DefaultLoRaDataRequestHandler(this.configuration, this.frameCounterUpdateStrategyFactory, new LoRaPayloadDecoder()));
            return loRaDevice;
        }

        internal void SetClient(string devEUI, ILoRaDeviceClient deviceClient) => this.deviceClientMap[devEUI] = deviceClient;
    }
}