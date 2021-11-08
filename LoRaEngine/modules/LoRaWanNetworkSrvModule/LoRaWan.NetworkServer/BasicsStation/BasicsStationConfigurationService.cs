// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;

    internal sealed class BasicsStationConfigurationService : IBasicsStationConfigurationService, IDisposable
    {
        private const string RouterConfigPropertyName = "routerConfig";
        private const string CachePrefixName = "routerConfig:";
        
        private static readonly TimeSpan CacheTimeout = TimeSpan.FromHours(2);
        private readonly SemaphoreSlim cacheSemaphore = new SemaphoreSlim(1);
        private readonly LoRaDeviceAPIServiceBase loRaDeviceApiService;
        private readonly ILoRaDeviceFactory loRaDeviceFactory;
        private readonly IMemoryCache cache;

        public BasicsStationConfigurationService(LoRaDeviceAPIServiceBase loRaDeviceApiService,
                                                 ILoRaDeviceFactory loRaDeviceFactory,
                                                 IMemoryCache cache)
        {
            this.loRaDeviceApiService = loRaDeviceApiService;
            this.loRaDeviceFactory = loRaDeviceFactory;
            this.cache = cache;
        }

        public void Dispose() => this.cacheSemaphore.Dispose();

        public async Task<Region> GetRegionAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var config = await this.GetRouterConfigMessageAsync(stationEui, cancellationToken);
            return LnsStationConfiguration.GetRegion(config);
        }

        public async Task<string> GetRouterConfigMessageAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var cacheKey = $"{CachePrefixName}{stationEui}";

            if (this.cache.TryGetValue(cacheKey, out var result))
                return (string)result;

            await this.cacheSemaphore.WaitAsync(cancellationToken);

            try
            {
                return await this.cache.GetOrCreateAsync(cacheKey, cacheEntry =>
                {
                    _ = cacheEntry.SetAbsoluteExpiration(CacheTimeout);
                    return GetRouterConfigMessageInternalAsync(stationEui);
                });
            }
            finally
            {
                _ = this.cacheSemaphore.Release();
            }
        }

        private async Task<string> GetRouterConfigMessageInternalAsync(StationEui stationEui)
        {
            var queryResult = await this.loRaDeviceApiService.SearchByDevEUIAsync(stationEui.ToString());
            if (queryResult.Count != 1)
            {
                throw new LoRaProcessingException($"The configuration request of station '{stationEui}' did not match any configuration in IoT Hub. If you expect this connection request to succeed, make sure to provision the Basics Station in the device registry.",
                                                  LoRaProcessingErrorCode.InvalidDeviceConfiguration);
            }

            var info = queryResult.Single();
            using var client = this.loRaDeviceFactory.CreateDeviceClient(info.DevEUI, info.PrimaryKey);
            var twin = await client.GetTwinAsync();

            try
            {
                var configJson = ((object)twin.Properties.Desired[RouterConfigPropertyName]).ToString();
                return LnsStationConfiguration.GetConfiguration(configJson);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new LoRaProcessingException($"Property '{RouterConfigPropertyName}' was not present in device twin.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);
            }
        }
    }
}
