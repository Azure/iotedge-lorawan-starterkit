// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System.Collections.Generic;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;

    internal class TestLoRaDeviceFactory : ILoRaDeviceFactory
    {
        private readonly ILoRaDeviceClient loRaDeviceClient;
        private readonly ILoRaDataRequestHandler requestHandler;
        private readonly Dictionary<string, ILoRaDeviceClient> deviceClientMap;
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private readonly IDeduplicationStrategyFactory deduplicationFactory;
        private readonly ILoRaADRStrategyProvider adrStrategyProvider;
        private readonly ILoRAADRManagerFactory adrManagerFactory;

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
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            ILoRaDeviceClient loRaDeviceClient,
            IDeduplicationStrategyFactory deduplicationFactory,
            ILoRaADRStrategyProvider adrStrategyProvider,
            ILoRAADRManagerFactory adrManagerFactory)
            : this(loRaDeviceClient)
        {
            this.configuration = configuration;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
            this.deduplicationFactory = deduplicationFactory;
            this.adrStrategyProvider = adrStrategyProvider;
            this.adrManagerFactory = adrManagerFactory;
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

            loRaDevice.SetRequestHandler(this.requestHandler ?? new DefaultLoRaDataRequestHandler(this.configuration, this.frameCounterUpdateStrategyProvider, new LoRaPayloadDecoder(), this.deduplicationFactory, this.adrStrategyProvider, this.adrManagerFactory));
            return loRaDevice;
        }

        internal void SetClient(string devEUI, ILoRaDeviceClient deviceClient) => this.deviceClientMap[devEUI] = deviceClient;
    }
}