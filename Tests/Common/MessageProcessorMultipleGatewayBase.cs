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
    using Xunit;

    public class MessageProcessorMultipleGatewayBase : MessageProcessorTestBase
    {
        private const string SecondServerGatewayID = "second-gateway";

        private readonly MemoryCache cache;

        public NetworkServerConfiguration SecondServerConfiguration { get; }

        public TestPacketForwarder SecondPacketForwarder { get; }

        public Mock<LoRaDeviceAPIServiceBase> SecondLoRaDeviceApi { get; }

        public LoRaDeviceFrameCounterUpdateStrategyProvider SecondFrameCounterUpdateStrategyProvider { get; }

        protected DefaultLoRaDataRequestHandler SecondRequestHandlerImplementation { get; }

        public Mock<ILoRaDeviceClient> SecondLoRaDeviceClient { get; }

        public LoRaDeviceClientConnectionManager SecondConnectionManager { get; }

        protected TestLoRaDeviceFactory SecondLoRaDeviceFactory { get; }

        public MessageProcessorMultipleGatewayBase()
        {
            SecondServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = SecondServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(CultureInfo.InvariantCulture),
            };

            SecondPacketForwarder = new TestPacketForwarder();
            SecondLoRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            SecondFrameCounterUpdateStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(SecondServerGatewayID, SecondLoRaDeviceApi.Object);
            var deduplicationStrategyFactory = new DeduplicationStrategyFactory(SecondLoRaDeviceApi.Object);
            var loRaAdrManagerFactory = new LoRAADRManagerFactory(SecondLoRaDeviceApi.Object);
            var adrStrategyProvider = new LoRaADRStrategyProvider();
            var functionBundlerProvider = new FunctionBundlerProvider(SecondLoRaDeviceApi.Object);
            SecondRequestHandlerImplementation = new DefaultLoRaDataRequestHandler(SecondServerConfiguration, SecondFrameCounterUpdateStrategyProvider, new LoRaPayloadDecoder(), deduplicationStrategyFactory, adrStrategyProvider, loRaAdrManagerFactory, functionBundlerProvider);
            SecondLoRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });
            SecondConnectionManager = new LoRaDeviceClientConnectionManager(this.cache);
            SecondLoRaDeviceFactory = new TestLoRaDeviceFactory(SecondServerConfiguration, SecondFrameCounterUpdateStrategyProvider, SecondLoRaDeviceClient.Object, deduplicationStrategyFactory, adrStrategyProvider, loRaAdrManagerFactory, functionBundlerProvider, SecondConnectionManager);
        }

        // Protected implementation of Dispose pattern.
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this.cache.Dispose();
            }
        }
    }
}
