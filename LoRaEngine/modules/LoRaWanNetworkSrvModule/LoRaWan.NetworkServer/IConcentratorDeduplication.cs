// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public interface IConcentratorDeduplication<T> where T : class
    {
        /// <summary>
        /// Detects messages that should be dropped based on whether
        /// they were encountered from the same or a different concentrator before.
        /// </summary>
        /// <param name="message">The received message.</param>
        /// <param name="stationEui">The current station that the message was sent from.</param>
        /// <returns>True, if the message has been encountered in the past and should be dropped.</returns>
        public bool ShouldDrop(T message, StationEui stationEui);
    }
}
