// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaWan.NetworkServer.BasicsStation;

    public interface IConcentratorDeduplication
    {
        public bool IsDuplicate(UpstreamDataFrame updf, StationEui stationEui);
    }
}
