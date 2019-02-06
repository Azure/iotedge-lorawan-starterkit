// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;
    using LoRaWan.NetworkServer;
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

        private DefaultLoRaDataRequestHandler requestHandlerImplementation;

        public TestPacketForwarder PacketForwarder { get; }

        protected Mock<LoRaDeviceAPIServiceBase> LoRaDeviceApi { get; }

        protected ILoRaDeviceFrameCounterUpdateStrategyFactory FrameCounterUpdateStrategyFactory { get; }

        protected NetworkServerConfiguration ServerConfiguration { get; }

        internal TestLoRaDeviceFactory LoRaDeviceFactory { get; }

        protected Mock<ILoRaDeviceClient> LoRaDeviceClient { get; }

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

            this.PacketForwarder = new TestPacketForwarder();
            this.LoRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            this.FrameCounterUpdateStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(ServerGatewayID, this.LoRaDeviceApi.Object);
            this.requestHandlerImplementation = new DefaultLoRaDataRequestHandler(this.ServerConfiguration, this.FrameCounterUpdateStrategyFactory, new LoRaPayloadDecoder());
            this.LoRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.LoRaDeviceFactory = new TestLoRaDeviceFactory(this.ServerConfiguration, this.FrameCounterUpdateStrategyFactory, this.LoRaDeviceClient.Object);
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

        public LoRaDevice CreateLoRaDevice(SimulatedDevice simulatedDevice) => TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.LoRaDeviceClient.Object, this.requestHandlerImplementation);

        public WaitableLoRaRequest CreateWaitableRequest(Rxpk rxpk, IPacketForwarder packetForwarder = null) => new WaitableLoRaRequest(rxpk, packetForwarder ?? this.PacketForwarder);
    }
}
