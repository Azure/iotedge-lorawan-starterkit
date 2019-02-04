// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Logging;
    using Moq;

    public class MessageProcessorTestBase
    {
        protected const string ServerGatewayID = "test-gateway";

        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategy> frameCounterUpdateStrategy;
        private readonly Mock<ILoRaDeviceFrameCounterUpdateStrategyFactory> frameCounterUpdateStrategyFactory;
        private readonly byte[] macAddress;
        private long startTime;
        private NetworkServerConfiguration serverConfiguration;

        protected NetworkServerConfiguration ServerConfiguration { get => this.serverConfiguration; }

        protected Mock<ILoRaDeviceFrameCounterUpdateStrategy> FrameCounterUpdateStrategy => this.frameCounterUpdateStrategy;

        protected Mock<ILoRaDeviceFrameCounterUpdateStrategyFactory> FrameCounterUpdateStrategyFactory { get => this.frameCounterUpdateStrategyFactory; }

        private readonly Mock<ILoRaDeviceRegistry> loRaDeviceRegistry;

        protected Mock<ILoRaDeviceRegistry> LoRaDeviceRegistry => this.loRaDeviceRegistry;

        protected Task<Message> EmptyAdditionalMessageReceiveAsync => Task.Delay(LoRaOperationTimeWatcher.MinimumAvailableTimeToCheckForCloudMessage).ContinueWith((_) => (Message)null);

        public MessageProcessorTestBase()
        {
            this.startTime = DateTimeOffset.UtcNow.Ticks;

            this.macAddress = Utility.GetMacAddress();
            this.serverConfiguration = new NetworkServerConfiguration
            {
                GatewayID = ServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(),
            };

            this.frameCounterUpdateStrategy = new Mock<ILoRaDeviceFrameCounterUpdateStrategy>(MockBehavior.Strict);
            this.frameCounterUpdateStrategyFactory = new Mock<ILoRaDeviceFrameCounterUpdateStrategyFactory>(MockBehavior.Strict);
            this.loRaDeviceRegistry = new Mock<ILoRaDeviceRegistry>(MockBehavior.Strict);
            this.loRaDeviceRegistry.Setup(x => x.RegisterDeviceInitializer(It.IsNotNull<ILoRaDeviceInitializer>()));
        }
    }
}
