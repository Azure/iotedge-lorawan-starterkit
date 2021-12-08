// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Globalization;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;

    public class MessageProcessorMultipleGatewayBase : MessageProcessorTestBase
    {
        private const string SecondServerGatewayID = "second-gateway";

        private readonly MemoryCache cache;

        public NetworkServerConfiguration SecondServerConfiguration { get; }

        public TestPacketForwarder SecondPacketForwarder { get; }

        public Mock<LoRaDeviceAPIServiceBase> SecondLoRaDeviceApi { get; }

        public LoRaDeviceFrameCounterUpdateStrategyProvider SecondFrameCounterUpdateStrategyProvider { get; }

        public ConcentratorDeduplication SecondConcentratorDeduplication { get; }

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
            SecondFrameCounterUpdateStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(SecondServerConfiguration, SecondLoRaDeviceApi.Object);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });

            var deduplicationStrategyFactory = new DeduplicationStrategyFactory(NullLoggerFactory.Instance, NullLogger<DeduplicationStrategyFactory>.Instance);
            var loRaAdrManagerFactory = new LoRAADRManagerFactory(SecondLoRaDeviceApi.Object, NullLoggerFactory.Instance);
            var adrStrategyProvider = new LoRaADRStrategyProvider(NullLoggerFactory.Instance);
            var functionBundlerProvider = new FunctionBundlerProvider(SecondLoRaDeviceApi.Object, NullLoggerFactory.Instance, NullLogger<FunctionBundlerProvider>.Instance);
            SecondConcentratorDeduplication = new ConcentratorDeduplication(this.cache, NullLogger<IConcentratorDeduplication>.Instance);

            SecondRequestHandlerImplementation = new DefaultLoRaDataRequestHandler(SecondServerConfiguration, SecondFrameCounterUpdateStrategyProvider, SecondConcentratorDeduplication, new LoRaPayloadDecoder(NullLogger<LoRaPayloadDecoder>.Instance), deduplicationStrategyFactory, adrStrategyProvider, loRaAdrManagerFactory, functionBundlerProvider, NullLogger<DefaultLoRaDataRequestHandler>.Instance, null);
            SecondLoRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            SecondConnectionManager = new LoRaDeviceClientConnectionManager(this.cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);
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
