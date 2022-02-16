// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServerDiscovery
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Identity;
    using LoRaTools;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;

    public sealed class TagBasedLnsDiscovery : ILnsDiscovery, IDisposable
    {
        private const string IotHubConnectionStringName = "IotHub";
        private const string HostName = "IotHubHostName";
        private const string NetworkTagName = "network";
        private const string CacheKey = "LnsUriByNetworkId";

        private static readonly IJsonReader<Uri> HostAddressReader =
            JsonReader.Object(JsonReader.Property("hostAddress", from s in JsonReader.String()
                                                                 select new Uri(s)));

        private readonly ILogger<TagBasedLnsDiscovery> logger;
        private readonly IMemoryCache memoryCache;
        private readonly RegistryManager registryManager;
        private readonly Dictionary<StationEui, Uri> lastLnsUriByStationId = new();
        private readonly object lastLnsUriByStationIdLock = new();
        private readonly SemaphoreSlim cacheSemaphore = new SemaphoreSlim(1);

        public TagBasedLnsDiscovery(IMemoryCache memoryCache, IConfiguration configuration, ILogger<TagBasedLnsDiscovery> logger)
            : this(memoryCache, InitializeRegistryManager(configuration, logger), logger)
        { }

        private static RegistryManager InitializeRegistryManager(IConfiguration configuration, ILogger logger)
        {
            var iotHubConnectionString = configuration.GetConnectionString(IotHubConnectionStringName);
            if (!string.IsNullOrEmpty(iotHubConnectionString))
            {
                logger.LogInformation("Using connection string-based auth for IoT Hub.");
                return RegistryManager.CreateFromConnectionString(iotHubConnectionString);
            }
            else
            {
                var hostName = configuration.GetValue<string>(HostName);

                if (string.IsNullOrEmpty(hostName))
                    throw new InvalidOperationException($"Specify either 'ConnectionStrings__{IotHubConnectionStringName}' or '{HostName}'.");

                logger.LogInformation("Using managed identity-based auth for IoT Hub.");
                return RegistryManager.Create(hostName, new ManagedIdentityCredential());
            }
        }

        internal TagBasedLnsDiscovery(IMemoryCache memoryCache, RegistryManager registryManager, ILogger<TagBasedLnsDiscovery> logger)
        {
            this.memoryCache = memoryCache;
            this.registryManager = registryManager;
            this.logger = logger;
        }

        public async Task<Uri> ResolveLnsAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var twin = await this.registryManager.GetTwinAsync(stationEui.ToString(), cancellationToken);

            if (twin is null)
                throw new LoRaProcessingException($"Could not find twin for station '{stationEui}'", LoRaProcessingErrorCode.TwinFetchFailed);

            var reader = new TwinCollectionReader(twin.Tags, this.logger);
            var networkId = reader.ReadRequiredString(NetworkTagName);

            if (networkId.Any(n => !char.IsLetterOrDigit(n)))
                throw new LoRaProcessingException("Network ID may not be empty and only contain alphanumeric characters.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);

            // Semaphore over all cache operations.
            // Once the LNS are loaded by network ID, these operations should return immediately.
            // We wait on the semaphore to avoid having two concurrent requests to the registry for the same network ID.
            await this.cacheSemaphore.WaitAsync(cancellationToken);

            List<Uri> lnsUris;
            try
            {
                lnsUris = await this.memoryCache.GetOrCreateAsync($"{CacheKey}:{networkId}", async _ =>
                {
                    var query = this.registryManager.CreateQuery($"SELECT properties.desired.hostAddress FROM devices.modules WHERE tags.network = '{networkId}'");
                    var results = new List<Uri>();
                    while (query.HasMoreResults)
                    {
                        var matches = await query.GetNextAsJsonAsync();
                        results.AddRange(matches.Select(hostAddressInfo => HostAddressReader.Read(hostAddressInfo)));
                    }

                    // Also cache if no LNS URIs are found for the given network.
                    // This makes sure that rogue LBS do not cause too many registry operations.
                    return results;
                });
            }
            finally
            {
                _ = this.cacheSemaphore.Release();
            }

            if (lnsUris.Count == 0)
                throw new LoRaProcessingException($"No LNS found in network '{networkId}'.", LoRaProcessingErrorCode.LnsDiscoveryFailed);

            lock (this.lastLnsUriByStationIdLock)
            {
                var next = this.lastLnsUriByStationId.TryGetValue(stationEui, out var lastLnsUri) ? lnsUris[(lnsUris.FindIndex(u => u == lastLnsUri) + 1) % lnsUris.Count] : lnsUris[0];
                this.lastLnsUriByStationId[stationEui] = next;
                return next;
            }
        }

        public void Dispose() => this.cacheSemaphore.Dispose();
    }
}
