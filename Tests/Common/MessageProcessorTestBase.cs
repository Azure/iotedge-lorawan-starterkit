// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit.Abstractions;

    public class MessageProcessorTestBase : IDisposable
    {
        public const string ServerGatewayID = "test-gateway";

        private readonly MemoryCache cache;
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;
        private readonly byte[] macAddress;
        private readonly long startTime;
        private bool disposedValue;

        public TestPacketForwarder PacketForwarder { get; }

        protected Region DefaultRegion { get; }

        protected Mock<LoRaDeviceAPIServiceBase> LoRaDeviceApi { get; }

        protected ILoRaDeviceFrameCounterUpdateStrategyProvider FrameCounterUpdateStrategyProvider { get; }

        protected NetworkServerConfiguration ServerConfiguration { get; }

        public TestLoRaDeviceFactory LoRaDeviceFactory { get; }

        protected Mock<ILoRaDeviceClient> LoRaDeviceClient { get; }

        protected TestLoRaPayloadDecoder PayloadDecoder { get; }

        protected DefaultLoRaDataRequestHandler RequestHandlerImplementation { get; }

        protected static Task<Message> EmptyAdditionalMessageReceiveAsync => Task.Delay(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage).ContinueWith((_) => (Message)null, TaskScheduler.Default);

        protected LoRaDeviceClientConnectionManager ConnectionManager { get; }

        protected ConcentratorDeduplication ConcentratorDeduplication { get; }

        protected LoRaDeviceCache DeviceCache { get; init; }

        public MessageProcessorTestBase(ITestOutputHelper testOutputHelper)
        {
            this.startTime = DateTimeOffset.UtcNow.Ticks;

            this.macAddress = Utility.GetMacAddress();
            ServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = ServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(CultureInfo.InvariantCulture),
            };

            this.testOutputLoggerFactory = new TestOutputLoggerFactory(testOutputHelper);
            PayloadDecoder = new TestLoRaPayloadDecoder(new LoRaPayloadDecoder(this.testOutputLoggerFactory.CreateLogger<LoRaPayloadDecoder>()));
            PacketForwarder = new TestPacketForwarder();
            LoRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            FrameCounterUpdateStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(ServerConfiguration, LoRaDeviceApi.Object);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });

            var deduplicationFactory = new DeduplicationStrategyFactory(this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<DeduplicationStrategyFactory>());
            var adrStrategyProvider = new LoRaADRStrategyProvider(this.testOutputLoggerFactory);
            var adrManagerFactory = new LoRAADRManagerFactory(LoRaDeviceApi.Object, this.testOutputLoggerFactory);
            var functionBundlerProvider = new FunctionBundlerProvider(LoRaDeviceApi.Object, this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<FunctionBundlerProvider>());

            LoRaDeviceClient = new Mock<ILoRaDeviceClient>();
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });
            ConnectionManager = new LoRaDeviceClientConnectionManager(this.cache, this.testOutputLoggerFactory.CreateLogger<LoRaDeviceClientConnectionManager>());
            ConcentratorDeduplication = new ConcentratorDeduplication(this.cache, this.testOutputLoggerFactory.CreateLogger<IConcentratorDeduplication>());
            RequestHandlerImplementation = new DefaultLoRaDataRequestHandler(ServerConfiguration,
                                                                             FrameCounterUpdateStrategyProvider,
                                                                             ConcentratorDeduplication,
                                                                             PayloadDecoder,
                                                                             deduplicationFactory,
                                                                             adrStrategyProvider,
                                                                             adrManagerFactory,
                                                                             functionBundlerProvider,
                                                                             this.testOutputLoggerFactory.CreateLogger<DefaultLoRaDataRequestHandler>(),
                                                                             null);

            var requestHandler = new DefaultLoRaDataRequestHandler(ServerConfiguration,
                                                                   FrameCounterUpdateStrategyProvider,
                                                                   ConcentratorDeduplication,
                                                                   new LoRaPayloadDecoder(this.testOutputLoggerFactory.CreateLogger<LoRaPayloadDecoder>()),
                                                                   deduplicationFactory,
                                                                   adrStrategyProvider,
                                                                   adrManagerFactory,
                                                                   functionBundlerProvider,
                                                                   this.testOutputLoggerFactory.CreateLogger<DefaultLoRaDataRequestHandler>(),
                                                                   meter: null);
            DeviceCache = new LoRaDeviceCache(new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.FromMilliseconds(int.MaxValue), RefreshInterval = TimeSpan.FromMilliseconds(int.MaxValue), ValidationInterval = TimeSpan.FromMilliseconds(int.MaxValue) },
                                              new NetworkServerConfiguration { GatewayID = ServerGatewayID },
                                              this.testOutputLoggerFactory.CreateLogger<LoRaDeviceCache>(),
                                              TestMeter.Instance);
            LoRaDeviceFactory = new TestLoRaDeviceFactory(ServerConfiguration, LoRaDeviceClient.Object, ConnectionManager, DeviceCache, requestHandler);

            // By default we pick EU868 region.
            DefaultRegion = Enum.TryParse<LoRaRegionType>(Environment.GetEnvironmentVariable("REGION"), out var loraRegionType) ?
                        (RegionManager.TryTranslateToRegion(loraRegionType, out var resolvedRegion) ? resolvedRegion : RegionManager.EU868) : RegionManager.EU868;
        }

        public static MemoryCache NewMemoryCache() => new MemoryCache(new MemoryCacheOptions());

        /// <summary>
        /// Creates a <see cref="IMemoryCache"/> containing the <paramref name="loRaDevice"/> already available.
        /// </summary>
        public static LoRaDeviceCache CreateDeviceCache(LoRaDevice loRaDevice)
        {
            var cache = LoRaDeviceCacheDefault.CreateDefault();
            cache.Register(loRaDevice);
            return cache;
        }

        public static IMemoryCache EmptyMemoryCache()
        {
            return new MemoryCache(new MemoryCacheOptions());
        }

        public LoRaDevice CreateLoRaDevice(SimulatedDevice simulatedDevice, bool registerConnection = true)
        {
            var device = TestUtils.CreateFromSimulatedDevice(simulatedDevice, ConnectionManager, RequestHandlerImplementation);
            if (registerConnection)
            {
                ConnectionManager.Register(device, LoRaDeviceClient.Object);
            }

            return device;
        }

        protected WaitableLoRaRequest CreateWaitableRequest(LoRaPayload loRaPayload,
                                                            IPacketForwarder packetForwarder = null,
                                                            TimeSpan? startTimeOffset = null,
                                                            TimeSpan? constantElapsedTime = null,
                                                            bool useRealTimer = false,
                                                            Region region = null)
        {
            var effectiveRegion = region ?? DefaultRegion;
            var upstreamFrequency = effectiveRegion switch
            {
                RegionCN470RP2 r => r.UpstreamJoinFrequenciesToDownstreamAndChannelIndex.Keys.First(),
                _ => effectiveRegion.RegionLimits.FrequencyRange.Min
            };
            // var upstreamFrequency = new Hertz((((ulong)(effectiveRegion.RegionLimits.FrequencyRange.Max - effectiveRegion.RegionLimits.FrequencyRange.Min)) / 2) + effectiveRegion.RegionLimits.FrequencyRange.Min.AsUInt64);
            return CreateWaitableRequest(TestUtils.GenerateTestRadioMetadata(frequency: upstreamFrequency),
                                         loRaPayload,
                                         packetForwarder,
                                         startTimeOffset,
                                         constantElapsedTime,
                                         useRealTimer,
                                         effectiveRegion);
        }
            

        protected WaitableLoRaRequest CreateWaitableRequest(RadioMetadata metadata,
                                                            LoRaPayload loRaPayload,
                                                            IPacketForwarder packetForwarder = null,
                                                            TimeSpan? startTimeOffset = null,
                                                            TimeSpan? constantElapsedTime = null,
                                                            bool useRealTimer = false,
                                                            Region region = null)
        {
            var effectiveRegion = region ?? DefaultRegion;
            var request = WaitableLoRaRequest.Create(metadata,
                                                     loRaPayload,
                                                     packetForwarder ?? PacketForwarder,
                                                     startTimeOffset,
                                                     constantElapsedTime,
                                                     useRealTimer,
                                                     effectiveRegion);

            request.SetRegion(effectiveRegion);
            return request;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.cache.Dispose();
                    this.DeviceCache.Dispose();
                    this.testOutputLoggerFactory.Dispose();
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
