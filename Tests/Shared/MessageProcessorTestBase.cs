// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Shared
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class MessageProcessorTestBase : IDisposable
    {
        protected const string ServerGatewayID = "test-gateway";

        private readonly MemoryCache cache;
        private readonly byte[] macAddress;
        private long startTime;
        private bool disposedValue;

        public TestPacketForwarder PacketForwarder { get; }

        protected Mock<LoRaDeviceAPIServiceBase> LoRaDeviceApi { get; }

        protected ILoRaDeviceFrameCounterUpdateStrategyProvider FrameCounterUpdateStrategyProvider { get; }

        protected NetworkServerConfiguration ServerConfiguration { get; }

        public TestLoRaDeviceFactory LoRaDeviceFactory { get; }

        protected Mock<ILoRaDeviceClient> LoRaDeviceClient { get; }

        protected TestLoRaPayloadDecoder PayloadDecoder { get; }

        protected DefaultLoRaDataRequestHandler RequestHandlerImplementation { get; }

        protected Task<Message> EmptyAdditionalMessageReceiveAsync => Task.Delay(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage).ContinueWith((_) => (Message)null);

        protected LoRaDeviceClientConnectionManager ConnectionManager { get; }

        public MessageProcessorTestBase()
        {
            this.startTime = DateTimeOffset.UtcNow.Ticks;

            this.macAddress = Utility.GetMacAddress();
            this.ServerConfiguration = new NetworkServerConfiguration
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

            this.PayloadDecoder = new TestLoRaPayloadDecoder(new LoRaPayloadDecoder());
            this.PacketForwarder = new TestPacketForwarder();
            this.LoRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.FrameCounterUpdateStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(ServerGatewayID, this.LoRaDeviceApi.Object);
            var deduplicationFactory = new DeduplicationStrategyFactory(this.LoRaDeviceApi.Object);
            var adrStrategyProvider = new LoRaADRStrategyProvider();
            var adrManagerFactory = new LoRAADRManagerFactory(this.LoRaDeviceApi.Object);
            var functionBundlerProvider = new FunctionBundlerProvider(this.LoRaDeviceApi.Object);
            this.RequestHandlerImplementation = new DefaultLoRaDataRequestHandler(this.ServerConfiguration, this.FrameCounterUpdateStrategyProvider, this.PayloadDecoder, deduplicationFactory, adrStrategyProvider, adrManagerFactory, functionBundlerProvider);
            this.LoRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });
            this.ConnectionManager = new LoRaDeviceClientConnectionManager(this.cache);
            this.LoRaDeviceFactory = new TestLoRaDeviceFactory(this.ServerConfiguration, this.FrameCounterUpdateStrategyProvider, this.LoRaDeviceClient.Object, deduplicationFactory, adrStrategyProvider, adrManagerFactory, functionBundlerProvider, this.ConnectionManager);
        }

        public MemoryCache NewMemoryCache() => new MemoryCache(new MemoryCacheOptions());

        /// <summary>
        /// Creates a <see cref="IMemoryCache"/> containing the <paramref name="loRaDevice"/> already available.
        /// </summary>
        public IMemoryCache NewNonEmptyCache(LoRaDevice loRaDevice)
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
            var device = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.ConnectionManager, this.RequestHandlerImplementation);
            this.ConnectionManager.Register(device, this.LoRaDeviceClient.Object);
            return device;
        }

        public WaitableLoRaRequest CreateWaitableRequest(Rxpk rxpk, IPacketForwarder packetForwarder = null, TimeSpan? startTimeOffset = null, TimeSpan? constantElapsedTime = null)
        {
            var requestStartTime = startTimeOffset.HasValue ? DateTime.UtcNow.Subtract(startTimeOffset.Value) : DateTime.UtcNow;
            var request = new WaitableLoRaRequest(rxpk, packetForwarder ?? this.PacketForwarder, requestStartTime);

            if (constantElapsedTime.HasValue)
            {
                Assert.True(RegionManager.TryResolveRegion(rxpk, out var region));
                var timeWatcher = new TestLoRaOperationTimeWatcher(region, constantElapsedTime.Value);
                request.UseTimeWatcher(timeWatcher);
            }

            return request;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.cache.Dispose();
                }

                disposedValue = true;
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
