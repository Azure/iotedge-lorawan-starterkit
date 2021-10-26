// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;

    public class MessageProcessorTestBase : IDisposable
    {
        protected const string ServerGatewayID = "test-gateway";

        private readonly MemoryCache cache;
        private readonly byte[] macAddress;
        private readonly long startTime;
        private bool disposedValue;

        public TestPacketForwarder PacketForwarder { get; }

        protected Mock<LoRaDeviceAPIServiceBase> LoRaDeviceApi { get; }

        protected ILoRaDeviceFrameCounterUpdateStrategyProvider FrameCounterUpdateStrategyProvider { get; }

        protected NetworkServerConfiguration ServerConfiguration { get; }

        public TestLoRaDeviceFactory LoRaDeviceFactory { get; }

        protected Mock<ILoRaDeviceClient> LoRaDeviceClient { get; }

        protected TestLoRaPayloadDecoder PayloadDecoder { get; }

        protected DefaultLoRaDataRequestHandler RequestHandlerImplementation { get; }

        protected static Task<Message> EmptyAdditionalMessageReceiveAsync => Task.Delay(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage).ContinueWith((_) => (Message)null, TaskScheduler.Default);

        protected LoRaDeviceClientConnectionManager ConnectionManager { get; }

        public MessageProcessorTestBase()
        {
            this.startTime = DateTimeOffset.UtcNow.Ticks;

            this.macAddress = Utility.GetMacAddress();
            ServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = ServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(CultureInfo.InvariantCulture),
            };

            Logger.Init(new LoggerConfiguration()
            {
                LogLevel = LogLevel.Debug,
                LogToConsole = true,
            });

            PayloadDecoder = new TestLoRaPayloadDecoder(new LoRaPayloadDecoder());
            PacketForwarder = new TestPacketForwarder();
            LoRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            FrameCounterUpdateStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(ServerGatewayID, LoRaDeviceApi.Object);
            var deduplicationFactory = new DeduplicationStrategyFactory(LoRaDeviceApi.Object);
            var adrStrategyProvider = new LoRaADRStrategyProvider();
            var adrManagerFactory = new LoRAADRManagerFactory(LoRaDeviceApi.Object);
            var functionBundlerProvider = new FunctionBundlerProvider(LoRaDeviceApi.Object);
            RequestHandlerImplementation = new DefaultLoRaDataRequestHandler(ServerConfiguration, FrameCounterUpdateStrategyProvider, PayloadDecoder, deduplicationFactory, adrStrategyProvider, adrManagerFactory, functionBundlerProvider);
            LoRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });
            ConnectionManager = new LoRaDeviceClientConnectionManager(this.cache);
            LoRaDeviceFactory = new TestLoRaDeviceFactory(ServerConfiguration, FrameCounterUpdateStrategyProvider, LoRaDeviceClient.Object, deduplicationFactory, adrStrategyProvider, adrManagerFactory, functionBundlerProvider, ConnectionManager);
        }

        public static MemoryCache NewMemoryCache() => new MemoryCache(new MemoryCacheOptions());

        /// <summary>
        /// Creates a <see cref="IMemoryCache"/> containing the <paramref name="loRaDevice"/> already available.
        /// </summary>
        public static IMemoryCache NewNonEmptyCache(LoRaDevice loRaDevice)
        {
            var cache = new MemoryCache(new MemoryCacheOptions());

            // add by dev addr
            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary.TryAdd(loRaDevice.DevEUI, loRaDevice);
            cache.Set(loRaDevice.DevAddr, dictionary);

            // add device by deveui
            cache.Set(LoRaDeviceRegistry.CacheKeyForDevEUIDevice(loRaDevice.DevEUI), loRaDevice);

            return cache;
        }

        public LoRaDevice CreateLoRaDevice(SimulatedDevice simulatedDevice)
        {
            var device = TestUtils.CreateFromSimulatedDevice(simulatedDevice, ConnectionManager, RequestHandlerImplementation);
            ConnectionManager.Register(device, LoRaDeviceClient.Object);
            return device;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.cache.Dispose();
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
