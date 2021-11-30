// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    public interface IConcentratorDeduplication
    {
        /// <summary>
        /// Detects requests that should be dropped based on whether
        /// they were encountered from the same or a different concentrator before.
        /// </summary>
        /// <param name="loRaRequest">The received request.</param>
        /// <param name="loRaDevice">The device it was sent from, needed only for <code>LoRaPayloadData</code> requests.</param>
        /// <returns><code>True</code>, if the request has been encountered in the past and should be dropped.</returns>
        public bool ShouldDrop(LoRaRequest loRaRequest, LoRaDevice? loRaDevice);
    }
}
