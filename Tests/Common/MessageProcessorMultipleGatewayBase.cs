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
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit.Abstractions;

    public class MessageProcessorMultipleGatewayBase : MessageProcessorTestBase
    {
        private const string SecondServerGatewayID = "second-gateway";

        private readonly MemoryCache cache;
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;
        private readonly MockHttpClientFactory httpClientFactory;

        public NetworkServerConfiguration SecondServerConfiguration { get; }

        public TestDownstreamMessageSender SecondDownstreamMessageSender { get; }

        public Mock<LoRaDeviceAPIServiceBase> SecondLoRaDeviceApi { get; }

        public LoRaDeviceFrameCounterUpdateStrategyProvider SecondFrameCounterUpdateStrategyProvider { get; }

        public ConcentratorDeduplication SecondConcentratorDeduplication { get; }

        protected DefaultLoRaDataRequestHandler SecondRequestHandlerImplementation { get; }

        public Mock<ILoRaDeviceClient> SecondLoRaDeviceClient { get; }

        public LoRaDeviceClientConnectionManager SecondConnectionManager { get; }

        protected TestLoRaDeviceFactory SecondLoRaDeviceFactory { get; }

        public MessageProcessorMultipleGatewayBase(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            SecondServerConfiguration = new NetworkServerConfiguration
            {
                GatewayID = SecondServerGatewayID,
                LogToConsole = true,
                LogLevel = ((int)LogLevel.Debug).ToString(CultureInfo.InvariantCulture),
            };

            SecondDownstreamMessageSender = new TestDownstreamMessageSender();
            SecondLoRaDeviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            SecondFrameCounterUpdateStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(SecondServerConfiguration, SecondLoRaDeviceApi.Object);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });

            this.testOutputLoggerFactory = new TestOutputLoggerFactory(testOutputHelper);
            var deduplicationStrategyFactory = new DeduplicationStrategyFactory(this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<DeduplicationStrategyFactory>());
            var loRaAdrManagerFactory = new LoRAADRManagerFactory(SecondLoRaDeviceApi.Object, this.testOutputLoggerFactory);
            var adrStrategyProvider = new LoRaADRStrategyProvider(this.testOutputLoggerFactory);
            var functionBundlerProvider = new FunctionBundlerProvider(SecondLoRaDeviceApi.Object, this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<FunctionBundlerProvider>());
            SecondConcentratorDeduplication = new ConcentratorDeduplication(this.cache, this.testOutputLoggerFactory.CreateLogger<IConcentratorDeduplication>());

            this.httpClientFactory = new MockHttpClientFactory();
            SecondRequestHandlerImplementation = new DefaultLoRaDataRequestHandler(SecondServerConfiguration,
                                                                                   SecondFrameCounterUpdateStrategyProvider,
                                                                                   SecondConcentratorDeduplication,
                                                                                   new LoRaPayloadDecoder(this.httpClientFactory, this.testOutputLoggerFactory.CreateLogger<LoRaPayloadDecoder>()),
                                                                                   deduplicationStrategyFactory,
                                                                                   adrStrategyProvider,
                                                                                   loRaAdrManagerFactory,
                                                                                   functionBundlerProvider,
                                                                                   this.testOutputLoggerFactory.CreateLogger<DefaultLoRaDataRequestHandler>(),
                                                                                   null);
            SecondLoRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            this.cache = new MemoryCache(new MemoryCacheOptions() { ExpirationScanFrequency = TimeSpan.FromSeconds(5) });

            var defaultRequestHandler = new DefaultLoRaDataRequestHandler(SecondServerConfiguration,
                                                                          SecondFrameCounterUpdateStrategyProvider,
                                                                          SecondConcentratorDeduplication,
                                                                          new LoRaPayloadDecoder(this.httpClientFactory, this.testOutputLoggerFactory.CreateLogger<LoRaPayloadDecoder>()),
                                                                          deduplicationStrategyFactory,
                                                                          adrStrategyProvider,
                                                                          loRaAdrManagerFactory,
                                                                          functionBundlerProvider,
                                                                          this.testOutputLoggerFactory.CreateLogger<DefaultLoRaDataRequestHandler>(),
                                                                          meter: null);

            SecondConnectionManager = new LoRaDeviceClientConnectionManager(this.cache, this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<LoRaDeviceClientConnectionManager>());
            SecondLoRaDeviceFactory = new TestLoRaDeviceFactory(SecondServerConfiguration, SecondLoRaDeviceClient.Object, SecondConnectionManager, DeviceCache, defaultRequestHandler);
        }

        // Protected implementation of Dispose pattern.
        protected override async ValueTask DisposeAsync(bool disposing)
        {
            await base.DisposeAsync(disposing);
            if (disposing)
            {
                this.cache.Dispose();
                this.testOutputLoggerFactory.Dispose();
                this.httpClientFactory.Dispose();
            }
        }
    }
}
