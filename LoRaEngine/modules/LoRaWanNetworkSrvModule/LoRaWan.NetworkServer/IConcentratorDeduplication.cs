// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;

    public interface IConcentratorDeduplication
    {
        /// <summary>
        /// Checks whether the specified request is a duplicate and should be allowed, dropped or skip confirmation.
        /// </summary>
        /// <param name="loRaRequest">The received request.</param>
        /// <param name="loRaDevice">The leaf device it was sent from, required only for <code>LoRaPayloadData</code> requests.</param>
        /// <returns>Task of <code>ConcentratorDeduplication.Result</code> with the result of detection.</returns>
        public Task<ConcentratorDeduplication.Result> CheckDuplicateAsync(LoRaRequest loRaRequest, LoRaDevice? loRaDevice);
    }
}
