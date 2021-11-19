// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public interface IConcentratorDeduplication<T> where T : class
    {
        /// <summary>
        /// Detects frames (messages) that should be dropped based on whether
        /// they were encountered from the same or a different concentrator before.
        /// </summary>
        /// <param name="frame">The received frame.</param>
        /// <param name="stationEui">The current station that the frame was sent from.</param>
        /// <returns><code>true</code>, if the frame has been encountered in the past and should be dropped.</returns>
        public bool ShouldDrop(T frame, StationEui stationEui);
    }
}
