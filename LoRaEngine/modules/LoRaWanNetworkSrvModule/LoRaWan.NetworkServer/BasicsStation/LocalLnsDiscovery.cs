// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.NetworkServerDiscovery;

    public sealed class LocalLnsDiscovery : ILnsDiscovery
    {
        private readonly Uri lnsUri;

        public LocalLnsDiscovery(Uri lnsUri) => this.lnsUri = lnsUri;

        public Task<Uri> ResolveLnsAsync(StationEui stationEui, CancellationToken cancellationToken) =>
            Task.FromResult(this.lnsUri);
    }
}
