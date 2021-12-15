// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    public interface IConcentratorDeduplication
    {
        /// <summary>
        /// Validates if a particular Join request was handled by this LNS before.
        /// </summary>
        /// <param name="loRaRequest">The Join request</param>
        /// <returns><see cref="ConcentratorDeduplicationResult.NotDuplicate"/> if this join request was not processed before
        /// on this LNS otherwise <see cref="ConcentratorDeduplicationResult.Duplicate"/></returns>
        public ConcentratorDeduplicationResult CheckDuplicateJoin(LoRaRequest loRaRequest);

        /// <summary>
        /// Validates if a particular telemetry message has been processed before by this LNS and
        /// if so, what continuation strategy we should use depending on the deduplication strategy of
        /// the device.
        /// </summary>
        /// <param name="loRaRequest">The telemetry request</param>
        /// <param name="loRaDevice">The device that sent the message</param>
        /// <returns>Any of the <see cref="ConcentratorDeduplicationResult"/> values described.</returns>
        public ConcentratorDeduplicationResult CheckDuplicateData(LoRaRequest loRaRequest, LoRaDevice loRaDevice);
    }

    public enum ConcentratorDeduplicationResult
    {
        /// <summary>
        /// First message on this LNS
        /// </summary>
        NotDuplicate,
        /// <summary>
        /// Duplicate message due to resubmit of a confirmed
        /// message of the same station (concentrator).
        /// </summary>
        DuplicateDueToResubmission,
        /// <summary>
        /// Detected as a duplicate but due to the DeduplicationStrategy (Mark and None),
        /// allowed upstream.
        /// </summary>
        DuplicateAllowUpstream,
        /// <summary>
        /// Message is a duplicate and does not need to be
        /// sent upstream.
        /// </summary>
        Duplicate
    }
}
