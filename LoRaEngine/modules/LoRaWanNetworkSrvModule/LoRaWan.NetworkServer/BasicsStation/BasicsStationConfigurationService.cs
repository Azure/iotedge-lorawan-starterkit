// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Jacob;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json.Linq;

    internal sealed class BasicsStationConfigurationService : IBasicsStationConfigurationService, IDisposable
    {
        internal const string RouterConfigPropertyName = "routerConfig";
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
                    var key = await this.loRaDeviceApiService.GetPrimaryKeyByEuiAsync(stationEui);
                    if (string.IsNullOrEmpty(key))
                    {
                        throw new LoRaProcessingException($"The configuration request of station '{stationEui}' did not match any configuration in IoT Hub. If you expect this connection request to succeed, make sure to provision the Basics Station in the device registry.",
                                                          LoRaProcessingErrorCode.InvalidDeviceConfiguration);
                    }

                    await using var client = this.loRaDeviceFactory.CreateDeviceClient(stationEui.ToString(), key);
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
            => LnsStationConfiguration.GetConfiguration(await GetDesiredPropertyStringAsync(stationEui, RouterConfigPropertyName, cancellationToken));

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
            => JsonSerializer.Deserialize<CupsTwinInfo>(await GetDesiredPropertyStringAsync(stationEui, CupsPropertyName, cancellationToken));

        private async Task<string> GetDesiredPropertyStringAsync(StationEui stationEui, string propertyName, CancellationToken cancellationToken)
        {
            var desiredProperties = await GetTwinDesiredPropertiesAsync(stationEui, cancellationToken);
            return desiredProperties.TryReadJsonBlock(propertyName, out var json)
                ? json
                : throw new LoRaProcessingException($"Property '{propertyName}' was not present in device twin.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);
        }

        public async Task SetReportedPackageVersionAsync(StationEui stationEui, string package, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(package))
            {
                this.logger.LogDebug($"Station did not report any 'package' field. Skipping reported property update.");
                return;
            }

            var key = await this.loRaDeviceApiService.GetPrimaryKeyByEuiAsync(stationEui);
            if (string.IsNullOrEmpty(key))
            {
                throw new LoRaProcessingException($"The configuration request of station '{stationEui}' did not match any configuration in IoT Hub. If you expect this connection request to succeed, make sure to provision the Basics Station in the device registry.",
                                                  LoRaProcessingErrorCode.InvalidDeviceConfiguration);
            }

            await using var client = this.loRaDeviceFactory.CreateDeviceClient(stationEui.ToString(), key);
            var twinCollection = new TwinCollection();
            twinCollection[TwinProperty.Package] = package;
            _ = await client.UpdateReportedPropertiesAsync(twinCollection, cancellationToken);
        }
    }
}
