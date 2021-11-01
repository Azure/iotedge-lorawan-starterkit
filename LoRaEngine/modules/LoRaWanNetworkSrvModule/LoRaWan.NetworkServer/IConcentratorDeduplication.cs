// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public interface IConcentratorDeduplication
    {
        bool IsDuplicate(LoRaRequest request, uint payloadFrameCounterAdjusted, string deviceEUI);
    }
}
