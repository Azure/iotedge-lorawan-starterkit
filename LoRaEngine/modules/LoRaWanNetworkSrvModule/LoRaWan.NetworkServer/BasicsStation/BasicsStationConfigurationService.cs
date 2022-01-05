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
        private const string DwellTimeConfigurationPropertyName = "desiredTxParams";
        private const string ConcentratorTwinCachePrefixName = "concentratorTwin:";
        internal const string CupsPropertyName = "cups";
        internal const string ClientThumbprintPropertyName = "clientThumbprint";

        private static readonly IJsonReader<DwellTimeSetting> DwellTimeConfigurationReader =
            JsonReader.Object(JsonReader.Property("downlinkDwellLimit", JsonReader.Boolean()),
                              JsonReader.Property("uplinkDwellLimit", JsonReader.Boolean()),
                              JsonReader.Property("eirp", JsonReader.UInt32()),
                              (downlinkDwellLimit, uplinkDwellLimit, eirp) => new DwellTimeSetting(downlinkDwellLimit, uplinkDwellLimit, eirp));

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

        private async Task<TwinCollection> GetTwinDesiredPropertiesAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var cacheKey = $"{ConcentratorTwinCachePrefixName}{stationEui}";

            if (this.cache.TryGetValue(cacheKey, out var result))
                return (TwinCollection)result;

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
                    var twin = await client.GetTwinAsync(cancellationToken);
                    return twin.Properties.Desired;
                });
            }
            finally
            {
                _ = this.cacheSemaphore.Release();
            }
        }

        public async Task<string> GetRouterConfigMessageAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var configJson = await GetDesiredPropertyStringAsync(stationEui, RouterConfigPropertyName, cancellationToken);
            return LnsStationConfiguration.GetConfiguration(configJson);
        }

        public async Task<Region> GetRegionAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var config = await GetRouterConfigMessageAsync(stationEui, cancellationToken);
            var region = LnsStationConfiguration.GetRegion(config);
            if (region is DwellTimeLimitedRegion someRegion)
            {
                var dwellTimeSettings = await GetDesiredPropertyStringAsync(stationEui, DwellTimeConfigurationPropertyName, cancellationToken);
                someRegion.DesiredDwellTimeSetting = DwellTimeConfigurationReader.Read(dwellTimeSettings);
            }
            return region;
        }

        public async Task<string[]> GetAllowedClientThumbprintsAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var thumbprintsArrayJson = await GetDesiredPropertyStringAsync(stationEui, ClientThumbprintPropertyName, cancellationToken);
            return JsonReader.Array(JsonReader.String()).Read(thumbprintsArrayJson);
        }

        public async Task<CupsTwinInfo> GetCupsConfigAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var cupsJson = await GetDesiredPropertyStringAsync(stationEui, CupsPropertyName, cancellationToken);
            return JsonSerializer.Deserialize<CupsTwinInfo>(cupsJson);
        }

        private async Task<string> GetDesiredPropertyStringAsync(StationEui stationEui, string propertyName, CancellationToken cancellationToken)
        {
            var desiredProperties = await GetTwinDesiredPropertiesAsync(stationEui, cancellationToken);
            return desiredProperties.Contains(propertyName)
                ? ((object)desiredProperties[propertyName]).ToString()
                : throw new LoRaProcessingException($"Property '{propertyName}' was not present in device twin.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);
        }
    }
}
