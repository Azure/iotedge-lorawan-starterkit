// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;

    internal interface IBasicsStationConfigurationService
    {
        Task<string> GetRouterConfigMessageAsync(StationEui stationEui, CancellationToken cancellationToken);
        Task<Region> GetRegionAsync(StationEui stationEui, CancellationToken cancellationToken);
        Task<string[]> GetAllowedClientThumbprintsAsync(StationEui stationEui, CancellationToken cancellationToken);
        Task<CupsUpdateInfo> GetCupsConfigAsync(StationEui stationEui, CancellationToken cancellationToken);
    }
}
