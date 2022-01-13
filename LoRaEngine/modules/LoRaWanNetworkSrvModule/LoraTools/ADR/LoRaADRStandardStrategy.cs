// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Linq;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// A strategy based on the standard ADR strategy.
    /// </summary>
    public sealed class LoRaADRStandardStrategy : ILoRaADRStrategy
    {
        private const int MarginDb = 5;
        private const int MaxTxPowerIndex = 0;

        /// <summary>
        /// Array to calculate nb Repetion given packet loss
        /// X axis min 5 %, 5-10 %, 10-30%, more than 30%
        /// Y axis currentNbRep 1, 2, 3.
        /// </summary>
        private readonly int[,] pktLossToNbRep = new int[4, 3]
        {
            { 1, 1, 2 },
            { 1, 2, 3 },
            { 2, 3, 3 },
            { 3, 3, 3 }
        };
        private readonly ILogger<LoRaADRStandardStrategy> logger;

        public int MinimumNumberOfResult => 20;

        public int DefaultTxPower => 0;

        int ILoRaADRStrategy.DefaultNbRep => 1;

        public LoRaADRStandardStrategy(ILogger<LoRaADRStandardStrategy> logger)
        {
            this.logger = logger;
        }

        public LoRaADRResult ComputeResult(LoRaADRTable table, float requiredSnr, DataRateIndex upstreamDataRate, int minTxPower, DataRateIndex maxDr)
        {
            // We do not have enough frame to calculate ADR. We can assume that a crash was the cause.
            if (table == null || table.Entries.Count < 20)
            {
                this.logger.LogDebug("ADR: not enough frames captured. Sending default power values");
                return null;
            }

            // This is the first contact case to harmonize the txpower state between device and server or the crash case.
            if (!table.CurrentNbRep.HasValue || !table.CurrentTxPower.HasValue)
            {
                this.logger.LogDebug("ADR: Sending the device default power values");
                return null;
            }

            // This is a case of standard ADR calculus.
            var newNbRep = ComputeNbRepetion(table.Entries[0].FCnt, table.Entries[LoRaADRTable.FrameCountCaptureCount - 1].FCnt, table.CurrentNbRep.GetValueOrDefault());
            (var newTxPowerIndex, var newDatarate) = GetPowerAndDRConfiguration(requiredSnr, upstreamDataRate, table.Entries.Max(x => x.Snr), table.CurrentTxPower.GetValueOrDefault(), minTxPower, maxDr);

            if (newNbRep != table.CurrentNbRep || newTxPowerIndex != table.CurrentTxPower || newDatarate != upstreamDataRate)
            {
                return new LoRaADRResult()
                {
                    DataRate = newDatarate,
                    NbRepetition = newNbRep,
                    TxPower = newTxPowerIndex
                };
            }

            return null;
        }

        private static (int txPower, DataRateIndex datarate) GetPowerAndDRConfiguration(float requiredSnr, DataRateIndex dataRate, double maxSnr, int currentTxPowerIndex, int minTxPowerIndex, DataRateIndex maxDr)
        {
            var snrMargin = maxSnr - requiredSnr - MarginDb;

            var computedDataRate = dataRate;

            var nStep = (int)snrMargin;

            while (nStep != 0)
            {
                if (nStep > 0)
                {
                    if (computedDataRate < maxDr)
                    {
                        computedDataRate++;
                    }

                    // txpower is an inverted scale, hence if we want to reduce
                    else if (currentTxPowerIndex < minTxPowerIndex)
                    {
                        currentTxPowerIndex++;
                    }

                    nStep--;
                    if (currentTxPowerIndex >= minTxPowerIndex)
                        return (minTxPowerIndex, computedDataRate);
                }
                else if (nStep < 0)
                {
                    if (currentTxPowerIndex > MaxTxPowerIndex)
                    {
                        currentTxPowerIndex--;
                        nStep++;
                    }
                    else
                    {
                        return (currentTxPowerIndex, computedDataRate);
                    }
                }
            }

            return (currentTxPowerIndex, computedDataRate);
        }

        private int ComputeNbRepetion(uint first, uint last, int currentNbRep)
        {
            double pktLossRate = (float)(last - first - 19) / (last - first);
            if (pktLossRate < 0.05)
            {
                return this.pktLossToNbRep[0, currentNbRep - 1];
            }

            if (pktLossRate is >= 0.05 and < 0.10)
            {
                return this.pktLossToNbRep[1, currentNbRep - 1];
            }

            if (pktLossRate is >= 0.10 and < 0.30)
            {
                return this.pktLossToNbRep[2, currentNbRep - 1];
            }

            return this.pktLossToNbRep[3, currentNbRep];
        }
    }
}
