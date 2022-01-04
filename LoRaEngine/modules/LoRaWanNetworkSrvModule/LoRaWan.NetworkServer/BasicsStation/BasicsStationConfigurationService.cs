// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    internal sealed class BasicsStationConfigurationService : IBasicsStationConfigurationService, IDisposable
    {
        private const string RouterConfigPropertyName = "routerConfig";
        private const string ConcentratorTwinCachePrefixName = "concentratorTwin:";
        internal const string CupsPropertyName = "cups";
        internal const string ClientThumbprintPropertyName = "clientThumbprint";

        private static readonly TimeSpan CacheTimeout = TimeSpan.FromHours(2);
        private readonly SemaphoreSlim cacheSemaphore = new SemaphoreSlim(1);
        private readonly LoRaDeviceAPIServiceBase loRaDeviceApiService;
        private readonly ILoRaDeviceFactory loRaDeviceFactory;
        private readonly IMemoryCache cache;
        private readonly ILogger<BasicsStationConfigurationService> logger;

        public BasicsStationConfigurationService(LoRaDeviceAPIServiceBase loRaDeviceApiService,
                                                 ILoRaDeviceFactory loRaDeviceFactory,
                                                 IMemoryCache cache,
                                                 ILogger<BasicsStationConfigurationService> logger)
        {
            this.loRaDeviceApiService = loRaDeviceApiService;
            this.loRaDeviceFactory = loRaDeviceFactory;
            this.cache = cache;
            this.logger = logger;
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
            var desiredProperties = await GetTwinDesiredPropertiesAsync(stationEui, cancellationToken);
            if (desiredProperties.Contains(RouterConfigPropertyName))
            {
                var configJson = ((object)desiredProperties[RouterConfigPropertyName]).ToString();
                return LnsStationConfiguration.GetConfiguration(configJson);
            }
            throw new LoRaProcessingException($"Property '{RouterConfigPropertyName}' was not present in device twin.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);
        }

        public async Task<Region> GetRegionAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var config = await GetRouterConfigMessageAsync(stationEui, cancellationToken);
            return LnsStationConfiguration.GetRegion(config);
        }

        public async Task<string[]> GetAllowedClientThumbprintsAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var desiredProperties = await GetTwinDesiredPropertiesAsync(stationEui, cancellationToken);
            if (desiredProperties.Contains(ClientThumbprintPropertyName))
            {
                try
                {
                    var thumbprints = (JArray)(object)desiredProperties[ClientThumbprintPropertyName];
                    return thumbprints.ToObject<string[]>();
                }
                catch (Exception ex) when (ex is InvalidCastException)
                {
                    throw new LoRaProcessingException($"'{ClientThumbprintPropertyName}' format is invalid. An array is expected.", ex, LoRaProcessingErrorCode.InvalidDeviceConfiguration);
                }
            }

            throw new LoRaProcessingException($"Property '{ClientThumbprintPropertyName}' was not present in device twin.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);
        }

        public async Task<CupsTwinInfo> GetCupsConfigAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var desiredProperties = await GetTwinDesiredPropertiesAsync(stationEui, cancellationToken);
            if (desiredProperties.Contains(CupsPropertyName))
            {
                var cupsJson = ((object)desiredProperties[CupsPropertyName]).ToString();
                return JsonSerializer.Deserialize<CupsTwinInfo>(cupsJson);
            }

            throw new LoRaProcessingException($"Property '{CupsPropertyName}' was not present in device twin.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);
        }
    }
}

