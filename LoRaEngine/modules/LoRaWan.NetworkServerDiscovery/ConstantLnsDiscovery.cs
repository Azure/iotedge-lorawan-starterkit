// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServerDiscovery
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class ConstantLnsDiscovery : ILnsDiscovery
    {
        private readonly Uri lnsUri;

        public ConstantLnsDiscovery(Uri lnsUri) => this.lnsUri = lnsUri;

        public Task<Uri> ResolveLnsAsync(StationEui stationEui, CancellationToken cancellationToken) =>
            Task.FromResult(this.lnsUri);
    }
}
