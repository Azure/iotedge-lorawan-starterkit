// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    public interface IConcentratorDeduplication
    {
        public ConcentratorDeduplicationResult CheckDuplicateJoin(LoRaRequest loRaRequest);
        public ConcentratorDeduplicationResult CheckDuplicateData(LoRaRequest loRaRequest, LoRaDevice loRaDevice);
    }

    public enum ConcentratorDeduplicationResult
    {
        NotDuplicate,
        DuplicateDueToResubmission,
        SoftDuplicateDueToDeduplicationStrategy, // detected as a duplicate but due to the DeduplicationStrategy, marked only as a "soft" duplicate
        Duplicate
    }
}
