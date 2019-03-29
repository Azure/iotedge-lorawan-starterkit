// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    /// <summary>
    /// An interface implementing LoRa ADR strategies.
    /// </summary>
    public interface ILoRaADRStrategy
    {
        LoRaADRResult ComputeResult(string devEUI, LoRaADRTable table, float requiredSnr, int upstreamDataRate, int minTxPower, int maxDr);

        int MinimumNumberOfResult { get; }

        int DefaultTxPower { get; }

        int DefaultNbRep { get; }
    }
}
