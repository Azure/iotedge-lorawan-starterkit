// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServerDiscovery
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Azure.Devices;
    using Microsoft.Extensions.Configuration;

    public sealed class TagBasedLnsDiscovery : ILnsDiscovery
    {
        private const string IotHubConnectionStringName = "IotHub";
        private const string NetworkTagName = "network";

        private static readonly IJsonReader<Uri> HostAddressReader =
            JsonReader.Object(JsonReader.Property("hostAddress", from s in JsonReader.String()
                                                                 select new Uri(s)));

        private readonly ILogger<TagBasedLnsDiscovery> logger;
        private readonly RegistryManager registryManager;

        public TagBasedLnsDiscovery(IConfiguration configuration, ILogger<TagBasedLnsDiscovery> logger)
            : this(RegistryManager.CreateFromConnectionString(configuration.GetConnectionString(IotHubConnectionStringName)), logger)
        { }

        internal TagBasedLnsDiscovery(RegistryManager registryManager, ILogger<TagBasedLnsDiscovery> logger)
        {
            this.registryManager = registryManager;
            this.logger = logger;
        }

        public async Task<Uri> ResolveLnsAsync(StationEui stationEui, CancellationToken cancellationToken)
        {
            var twin = await this.registryManager.GetTwinAsync(stationEui.ToString(), cancellationToken);
            var reader = new TwinCollectionReader(twin.Tags, this.logger);
            var networkId = reader.ReadRequiredString(NetworkTagName);

            if (networkId.Any(n => !char.IsLetterOrDigit(n)))
                throw new InvalidOperationException("Network ID may only contain alphanumeric characters.");

            var query = this.registryManager.CreateQuery($"SELECT properties.desired.hostAddress FROM devices.modules WHERE tags.network = '{networkId}'");
            var results = new List<Uri>();
            while (query.HasMoreResults)
            {
                var matches = await query.GetNextAsJsonAsync();
                results.AddRange(matches.Select(hostAddressInfo => HostAddressReader.Read(hostAddressInfo)));
            }

            return results.First();
        }
    }
}
