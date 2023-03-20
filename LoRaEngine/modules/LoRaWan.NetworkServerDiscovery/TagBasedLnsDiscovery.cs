// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServerDiscovery
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Identity;
    using Jacob;
    using LoRaTools;
    using LoRaTools.IoTHubImpl;
    using LoRaTools.NetworkServerDiscovery;
    using LoRaWan;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;

    public sealed class TagBasedLnsDiscovery : ILnsDiscovery, IDisposable
    {
        private const string IotHubConnectionStringName = "IotHub";
        private const string HostName = "IotHubHostName";
        private const string LnsByNetworkCacheKey = "LnsUriByNetwork";
        private const string NetworkByStationCacheKey = "NetworkByStation";

        private static readonly TimeSpan CacheItemExpiration = TimeSpan.FromHours(6);
        private static readonly IJsonReader<LnsHostAddressParseResult> HostAddressReader =
            JsonReader.Object(JsonReader.Property("hostAddress",
                                                  from s in JsonReader.String()
                                                  select Uri.TryCreate(s, UriKind.Absolute, out var uri)
                                                      && uri.Scheme is "ws" or "wss"
                                                       ? uri : null,
                                                  (true, null)),
                              JsonReader.Property("deviceId", JsonReader.String()),
                              (hostAddress, deviceId) => new LnsHostAddressParseResult(hostAddress, deviceId));

        private readonly ILogger<TagBasedLnsDiscovery> logger;
        private readonly IMemoryCache memoryCache;
        private readonly IDeviceRegistryManager registryManager;
        private readonly Dictionary<StationEui, Uri> lastLnsUriByStationId = new();
        private readonly object lastLnsUriByStationIdLock = new();
        private readonly SemaphoreSlim lnsByNetworkCacheSemaphore = new SemaphoreSlim(1);

        public TagBasedLnsDiscovery(IMemoryCache memoryCache, IConfiguration configuration, ILogger<TagBasedLnsDiscovery> logger, ILogger<IDeviceRegistryManager> registryManagerLogger, IHttpClientFactory httpClientFactory)
            : this(memoryCache, InitializeRegistryManager(configuration, logger, registryManagerLogger, httpClientFactory), logger)
        { }

        private static IDeviceRegistryManager InitializeRegistryManager(IConfiguration configuration, ILogger logger, ILogger<IDeviceRegistryManager> registryManagerLogger, IHttpClientFactory httpClientFactory)
        {
            var iotHubConnectionString = configuration.GetConnectionString(IotHubConnectionStringName);
            if (!string.IsNullOrEmpty(iotHubConnectionString))
            {
                logger.LogInformation("Using connection string based auth for IoT Hub.");
                return IoTHubRegistryManager.CreateWithProvider(() => RegistryManager.CreateFromConnectionString(iotHubConnectionString), httpClientFactory, registryManagerLogger);
            }

            var hostName = configuration.GetValue<string>(HostName);

            if (string.IsNullOrEmpty(hostName))
                throw new InvalidOperationException($"Specify either 'ConnectionStrings__{IotHubConnectionStringName}' or '{HostName}'.");

            logger.LogInformation("Using managed identity based auth for IoT Hub.");

            return IoTHubRegistryManager.CreateWithProvider(() =>
                RegistryManager.Create(hostName, new ManagedIdentityCredential()), httpClientFactory, registryManagerLogger);
        }

        internal TagBasedLnsDiscovery(IMemoryCache memoryCache, IDeviceRegistryManager registryManager, ILogger<TagBasedLnsDiscovery> logger)
        {
            this.memoryCache = memoryCache;
            this.registryManager = registryManager;
            this.logger = logger;
        }

        public async Task<Uri> ResolveLnsAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var twin =
                await GetOrCreateAsync($"{NetworkByStationCacheKey}:{stationEui}",
                                       _ =>
                                       {
                                           this.logger.LogInformation("Loaded twin for station '{Station}'", stationEui);
                                           return this.registryManager.GetStationTwinAsync(stationEui, cancellationToken);
                                       },
                                       null, cancellationToken);

            if (twin is null)
                throw new LoRaProcessingException($"Could not find twin for station '{stationEui}'", LoRaProcessingErrorCode.TwinFetchFailed);

            // Protect against SQL injection.
            if (twin.NetworkId.Any(n => !char.IsLetterOrDigit(n)))
                throw new LoRaProcessingException("Network ID may not be empty and only contain alphanumeric characters.", LoRaProcessingErrorCode.InvalidDeviceConfiguration);

            var lnsUris = await GetOrCreateAsync(
                $"{LnsByNetworkCacheKey}:{twin.NetworkId}",
                async _ =>
                {
                    var query = this.registryManager.FindLnsByNetworkId(twin.NetworkId);
                    var results = new List<Uri>();
                    var parseFailures = new List<string>();
                    while (query.HasMoreResults)
                    {
                        var matches = await query.GetNextPageAsync();

                        var parseResult = matches.Select(hostAddressInfo => HostAddressReader.Read(hostAddressInfo)).ToList();

                        results.AddRange(parseResult.Select(r => r.HostAddress)
                                                    .Where(hostAddress => hostAddress != null)
                                                    .Cast<Uri>());

                        parseFailures.AddRange(parseResult.Where(r => r.HostAddress is null)
                                                          .Select(r => r.DeviceId));
                    }

                    this.logger.LogInformation("Loaded {Count} LNS candidates for network '{NetworkId}'", results.Count, twin.NetworkId);

                    if (parseFailures.Count > 0)
                        this.logger.LogWarning("The following LNS in network '{NetworkId}' have a misconfigured host address: {DeviceIds}.", twin.NetworkId, string.Join(',', parseFailures));

                    // Also cache if no LNS URIs are found for the given network.
                    // This makes sure that rogue LBS do not cause too many registry operations.
                    return results;
                },
                this.lnsByNetworkCacheSemaphore,
                cancellationToken);

            if (lnsUris.Count == 0)
                throw new LoRaProcessingException($"No LNS found in network '{twin.NetworkId}'.", LoRaProcessingErrorCode.LnsDiscoveryFailed);

            lock (this.lastLnsUriByStationIdLock)
            {
                var next = this.lastLnsUriByStationId.TryGetValue(stationEui, out var lastLnsUri) ? lnsUris[(lnsUris.FindIndex(u => u == lastLnsUri) + 1) % lnsUris.Count] : lnsUris[0];
                this.lastLnsUriByStationId[stationEui] = next;
                return next;
            }
        }

        /// <summary>
        /// Unifies all cache operations by setting the same absolute expiration on all cache entries.
        /// By passing a semaphore it serializes all cache operations to avoid having two concurrent requests to the registry for the same operation.
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="key"></param>
        /// <param name="factory"></param>
        /// <param name="semaphore"></param>
        /// <param name="cancellationToken"></param>
        private async Task<TItem> GetOrCreateAsync<TItem>(string key, Func<ICacheEntry, Task<TItem>> factory, SemaphoreSlim? semaphore, CancellationToken cancellationToken)
        {
            if (semaphore is { } someSemaphore)
            {
                await someSemaphore.WaitAsync(cancellationToken);
            }

            try
            {
                return await this.memoryCache.GetOrCreateAsync(key, ce =>
                {
                    _ = ce.SetAbsoluteExpiration(CacheItemExpiration);
                    return factory(ce);
                });
            }
            finally
            {
                _ = semaphore?.Release();
            }
        }

        public void Dispose() => this.lnsByNetworkCacheSemaphore.Dispose();

        private record struct LnsHostAddressParseResult(Uri? HostAddress, string DeviceId);
    }
}
