// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Common
{
    using BasicStation;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.PacketForwarder;
    using LoRaWan.Shared;
    using Microsoft.Extensions.Caching.Memory;

    public class PhysicalClientResolver
    {
        public static IPhysicalClient Create()
        {
            var configuration = NetworkServerConfiguration.CreateFromEnviromentVariables();

            if (configuration.UseBasicStation)
            {
                return new BasicStation();
            }
            else
            {
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
    }
}
