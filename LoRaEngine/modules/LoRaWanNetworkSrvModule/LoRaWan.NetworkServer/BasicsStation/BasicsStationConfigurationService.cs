// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;

    internal sealed class BasicsStationConfigurationService : IBasicsStationConfigurationService, IDisposable
    {
        private const string RouterConfigPropertyName = "routerConfig";
        private const string ClientThumbprintPropertyName = "clientThumbprint";
        private const string ConcentratorTwinCachePrefixName = "concentratorTwin:";

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

        private async Task<Twin> GetTwinAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var cacheKey = $"{ConcentratorTwinCachePrefixName}{stationEui}";

            if (this.cache.TryGetValue(cacheKey, out var result))
                return (Twin)result;

            await this.cacheSemaphore.WaitAsync(cancellationToken);

            try
            {
                return await this.cache.GetOrCreateAsync(cacheKey, async cacheEntry =>
                {
                    _ = cacheEntry.SetAbsoluteExpiration(CacheTimeout);
                    var queryResult = await this.loRaDeviceApiService.SearchByEuiAsync(stationEui);
                    if (queryResult.Count != 1)
                    {
                        throw new LoRaProcessingException($"The configuration request of station '{stationEui}' did not match any configuration in IoT Hub. If you expect this connection request to succeed, make sure to provision the Basics Station in the device registry.",
                                                          LoRaProcessingErrorCode.InvalidDeviceConfiguration);
                    }
                    var info = queryResult[0];
                    using var client = this.loRaDeviceFactory.CreateDeviceClient(info.DevEUI, info.PrimaryKey);
                    return await client.GetTwinAsync();
                });
            }
            finally
            {
                _ = this.cacheSemaphore.Release();
            }
        }

        public async Task<string> GetRouterConfigMessageAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var twin = await GetTwinAsync(stationEui, cancellationToken);
            try
            {
                var configJson = twin.Properties.Desired[RouterConfigPropertyName].ToString();
                return LnsStationConfiguration.GetConfiguration(configJson);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new LoRaProcessingException($"Property '{RouterConfigPropertyName}' was not present in device twin.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);
            }
        }

        public async Task<Region> GetRegionAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var config = await GetRouterConfigMessageAsync(stationEui, cancellationToken);
            return LnsStationConfiguration.GetRegion(config);
        }

        public async Task<string[]> GetAllowedClientThumbprints(StationEui stationEui, CancellationToken cancellationToken)
        {
            var twin = await GetTwinAsync(stationEui, cancellationToken);
            try
            {
                var thumbprintsArrayJson = (string)twin.Properties.Desired[ClientThumbprintPropertyName].ToString();
                return JsonReader.Array(JsonReader.String()).Read(thumbprintsArrayJson);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new LoRaProcessingException($"Property '{ClientThumbprintPropertyName}' was not present in device twin.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);
            }
        }
    }
}

