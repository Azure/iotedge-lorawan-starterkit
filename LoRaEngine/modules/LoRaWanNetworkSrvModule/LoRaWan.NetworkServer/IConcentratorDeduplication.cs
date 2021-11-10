// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaWan.NetworkServer.BasicsStation;

    public interface IConcentratorDeduplication
    {
        /// <summary>
        /// Detects data frames that should be dropped based on whether
        /// they were encountered from the same or a different concentrator before.
        /// </summary>
        /// <param name="updf">The received message.</param>
        /// <param name="stationEui">The current station that the message was sent from.</param>
        /// <returns>True, if dataframe has been encountered in the past and should be dropped.</returns>
        public bool ShouldDrop(UpstreamDataFrame updf, StationEui stationEui);
    }
}
