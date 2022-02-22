// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.NetworkServerDiscovery
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan;

    public interface ILnsDiscovery
    {
        public const string EndpointName = "/router-info";

        Task<Uri> ResolveLnsAsync(StationEui stationEui, CancellationToken cancellationToken);
    }
}
