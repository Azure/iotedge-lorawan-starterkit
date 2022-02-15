// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServerDiscovery
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class LnsDiscovery
    {
        public const string EndpointName = "/router-info";
        private readonly Uri lnsUri;

        public LnsDiscovery(Uri lnsUri) => this.lnsUri = lnsUri;

#pragma warning disable IDE0060 // Remove unused parameter
        public Task<Uri> ResolveLnsAsync(StationEui stationEui, CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            return Task.FromResult(this.lnsUri);
        }
    }
}
