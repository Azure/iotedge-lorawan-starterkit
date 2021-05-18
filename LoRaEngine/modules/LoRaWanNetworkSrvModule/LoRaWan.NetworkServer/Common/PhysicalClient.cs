// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Common
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using BasicStation;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.PacketForwarder;
    using LoRaWan.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    public abstract class PhysicalClient : IPhysicalClient
    {
#pragma warning disable SA1401 // Fields should be private
        protected readonly NetworkServerConfiguration configuration;
        protected readonly MessageDispatcher messageDispatcher;
        protected readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        protected readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        protected ModuleClient ioTHubModuleClient;
        protected IClassCDeviceMessageSender classCMessageSender;
#pragma warning restore SA1401 // Fields should be private

        public PhysicalClient(
            NetworkServerConfiguration configuration,
            MessageDispatcher messageDispatcher,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceRegistry loRaDeviceRegistry)
        {
            this.configuration = configuration;
            this.messageDispatcher = messageDispatcher;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
        }

        /// <summary>
        /// Factory method to instantiate the correct UDP endpoint.
        /// </summary>
        public static IPhysicalClient Create()
        {
            var configuration = NetworkServerConfiguration.CreateFromEnviromentVariables();

            if (configuration.UseBasicStation)
            {
                Logger.Log("Using physical client with basic station implementation", LogLevel.Information);
                return new BasicStation();
            }
            else
            {
                Logger.Log("Using physical client with the packet forwarder implementation", LogLevel.Information);
                var loRaDeviceAPIService = new LoRaDeviceAPIService(configuration, new ServiceFacadeHttpClientProvider(configuration, ApiVersion.LatestVersion));
                var frameCounterStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(configuration.GatewayID, loRaDeviceAPIService);
                var deduplicationStrategyFactory = new DeduplicationStrategyFactory(loRaDeviceAPIService);
                var adrStrategyProvider = new LoRaADRStrategyProvider();
                var cache = new MemoryCache(new MemoryCacheOptions());
                var dataHandlerImplementation = new DefaultLoRaDataRequestHandler(configuration, frameCounterStrategyProvider, new LoRaPayloadDecoder(), deduplicationStrategyFactory, adrStrategyProvider, new LoRAADRManagerFactory(loRaDeviceAPIService), new FunctionBundlerProvider(loRaDeviceAPIService));
                var connectionManager = new LoRaDeviceClientConnectionManager(cache);
                var loRaDeviceFactory = new LoRaDeviceFactory(configuration, dataHandlerImplementation, connectionManager);
                var loRaDeviceRegistry = new LoRaDeviceRegistry(configuration, cache, loRaDeviceAPIService, loRaDeviceFactory);

                var messageDispatcher = new MessageDispatcher(configuration, loRaDeviceRegistry, frameCounterStrategyProvider);
                var udpServer = new UdpServer(configuration, messageDispatcher, loRaDeviceAPIService, loRaDeviceRegistry);

                // TODO: review dependencies
                var classCMessageSender = new DefaultClassCDevicesMessageSender(configuration, loRaDeviceRegistry, udpServer, frameCounterStrategyProvider);
                dataHandlerImplementation.SetClassCMessageSender(classCMessageSender);

                udpServer.SetClassCMessageSender(classCMessageSender);
                return udpServer;
            }
        }

        public abstract void Dispose();

        public abstract Task RunServer();
    }
}
