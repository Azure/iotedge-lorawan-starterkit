// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaTools.LoRaPhysical;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;

    public class MessageProcessorTestBase
    {
        protected const string ServerGatewayID = "test-gateway";

        private readonly byte[] macAddress;
        private long startTime;

        public TestPacketForwarder PacketForwarder { get; }

        protected Mock<LoRaDeviceAPIServiceBase> LoRaDeviceApi { get; }

        protected ILoRaDeviceFrameCounterUpdateStrategyProvider FrameCounterUpdateStrategyProvider { get; }

        protected NetworkServerConfiguration ServerConfiguration { get; }

        internal TestLoRaDeviceFactory LoRaDeviceFactory { get; }

        protected Mock<ILoRaDeviceClient> LoRaDeviceClient { get; }

        protected TestLoRaPayloadDecoder PayloadDecoder { get; }

        protected DefaultLoRaDataRequestHandler RequestHandlerImplementation { get; }

        protected Task<Message> EmptyAdditionalMessageReceiveAsync => Task.Delay(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage).ContinueWith((_) => (Message)null);

        public MessageProcessorTestBase()
        {
            this.startTime = DateTimeOffset.UtcNow.Ticks;

            this.macAddress = Utility.GetMacAddress();
            this.ServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = ServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(),
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
            this.LoRaDeviceFactory = new TestLoRaDeviceFactory(this.ServerConfiguration, this.FrameCounterUpdateStrategyProvider, this.LoRaDeviceClient.Object, deduplicationFactory, adrStrategyProvider, adrManagerFactory, functionBundlerProvider);
        }

        public MemoryCache NewMemoryCache() => new MemoryCache(new MemoryCacheOptions());

        /// <summary>
        /// Creates a <see cref="IMemoryCache"/> containing the <paramref name="loRaDevice"/> already available
        /// </summary>
        public IMemoryCache NewNonEmptyCache(LoRaDevice loRaDevice)
        {
            var cache = new MemoryCache(new MemoryCacheOptions());

            var dictionary = new DevEUIToLoRaDeviceDictionary();
            dictionary.TryAdd(loRaDevice.DevEUI, loRaDevice);
            cache.Set(loRaDevice.DevAddr, dictionary);

            return cache;
        }

        public LoRaDevice CreateLoRaDevice(SimulatedDevice simulatedDevice) => TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.LoRaDeviceClient.Object, this.RequestHandlerImplementation);

        public WaitableLoRaRequest CreateWaitableRequest(Rxpk rxpk, IPacketForwarder packetForwarder = null) => new WaitableLoRaRequest(rxpk, packetForwarder ?? this.PacketForwarder);
    }
}
