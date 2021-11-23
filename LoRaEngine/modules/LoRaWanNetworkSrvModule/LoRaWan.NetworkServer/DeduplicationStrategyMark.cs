// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using Microsoft.Extensions.Logging;

    public class DeduplicationStrategyMark : ILoRaDeviceMessageDeduplicationStrategy
    {
        public DeduplicationStrategyMark(ILogger<DeduplicationStrategyMark> logger)
        {
            logger.LogDebug("deduplication Strategy: Mark");
        }

        public DeduplicationResult Process(DeduplicationResult result, uint fCntUp)
        {
            if (result is null) throw new ArgumentNullException(nameof(result));

            result.CanProcess = true; // can always process. Message is marked if it is a duplicate
            return result;
        }
    }
}
