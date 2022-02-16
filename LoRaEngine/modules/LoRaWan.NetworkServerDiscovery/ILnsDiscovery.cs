// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServerDiscovery
{
    public interface ILnsDiscovery
    {
        public const string EndpointName = "/router-info";

        Task<Uri> ResolveLnsAsync(StationEui stationEui, CancellationToken cancellationToken);
    }
}
