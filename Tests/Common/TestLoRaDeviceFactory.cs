// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;

    public sealed class TestLoRaDeviceFactory : ILoRaDeviceFactory, IDisposable
    {
        private readonly ILoRaDeviceClient loRaDeviceClient;
        private readonly ILoRaDataRequestHandler requestHandler;
        private readonly IDictionary<string, ILoRaDeviceClient> deviceClientMap = new Dictionary<string, ILoRaDeviceClient>();
        private readonly IDictionary<string, LoRaDevice> deviceMap = new Dictionary<string, LoRaDevice>();
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private readonly IDeduplicationStrategyFactory deduplicationFactory;
        private readonly ILoRaADRStrategyProvider adrStrategyProvider;
        private readonly ILoRAADRManagerFactory adrManagerFactory;
        private readonly IFunctionBundlerProvider functionBundlerProvider;
        private readonly ILoRaDeviceClientConnectionManager connectionManager;
        private readonly IMemoryCache memoryCache;
        private ConcentratorDeduplication concentratorDeduplication;

        public TestLoRaDeviceFactory(ILoRaDeviceClient loRaDeviceClient, ILoRaDeviceClientConnectionManager connectionManager = null)
        {
            this.loRaDeviceClient = loRaDeviceClient;
            this.memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                ExpirationScanFrequency = TimeSpan.FromSeconds(5),
            });
            this.connectionManager = connectionManager ?? new LoRaDeviceClientConnectionManager(this.memoryCache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);
        }

        public TestLoRaDeviceFactory(ILoRaDeviceClient loRaDeviceClient, ILoRaDataRequestHandler requestHandler, ILoRaDeviceClientConnectionManager connectionManager = null)
        {
            this.loRaDeviceClient = loRaDeviceClient;
            this.requestHandler = requestHandler;
            this.memoryCache = new MemoryCache(new MemoryCacheOptions
            {
                ExpirationScanFrequency = TimeSpan.FromSeconds(5),
            });
            this.connectionManager = connectionManager ?? new LoRaDeviceClientConnectionManager(this.memoryCache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);
        }

        public TestLoRaDeviceFactory(
            NetworkServerConfiguration configuration,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            ILoRaDeviceClient loRaDeviceClient,
            IDeduplicationStrategyFactory deduplicationFactory,
            ILoRaADRStrategyProvider adrStrategyProvider,
            ILoRAADRManagerFactory adrManagerFactory,
            IFunctionBundlerProvider functionBundlerProvider,
            ILoRaDeviceClientConnectionManager connectionManager)
            : this(loRaDeviceClient)
        {
            this.configuration = configuration;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;
            this.deduplicationFactory = deduplicationFactory;
            this.adrStrategyProvider = adrStrategyProvider;
            this.adrManagerFactory = adrManagerFactory;
            this.functionBundlerProvider = functionBundlerProvider;
            this.connectionManager = connectionManager;
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
                this.connectionManager)
            {
                GatewayID = deviceInfo.GatewayId
            };

            this.connectionManager.Register(loRaDevice, deviceClientToAssign);

            this.concentratorDeduplication = new ConcentratorDeduplication(this.memoryCache, NullLogger<IConcentratorDeduplication>.Instance);

            loRaDevice.SetRequestHandler(this.requestHandler ?? new DefaultLoRaDataRequestHandler(this.configuration, this.frameCounterUpdateStrategyProvider, this.concentratorDeduplication, new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance), this.deduplicationFactory, this.adrStrategyProvider, this.adrManagerFactory, this.functionBundlerProvider, NullLogger<DefaultLoRaDataRequestHandler>.Instance, null));

            this.deviceMap[deviceInfo.DevEUI] = loRaDevice;

            return loRaDevice;
        }

        public bool TryGetLoRaDevice(string devEUI, out LoRaDevice device) => this.deviceMap.TryGetValue(devEUI, out device);

        public void SetClient(string devEUI, ILoRaDeviceClient deviceClient) => this.deviceClientMap[devEUI] = deviceClient;

        public void Dispose()
        {
            this.memoryCache.Dispose();
            this.concentratorDeduplication?.Dispose();
        }

        public ILoRaDeviceClient CreateDeviceClient(string eui, string primaryKey) =>
            this.deviceClientMap.TryGetValue(eui, out var deviceClientToAssign) ? deviceClientToAssign : this.loRaDeviceClient;
    }
}
