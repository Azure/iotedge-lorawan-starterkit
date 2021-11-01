// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IBasicsStationConfigurationService
    {
        Task<string> GetRouterConfigMessageAsync(StationEui stationEui, CancellationToken cancellationToken);
    }
}
