// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using System.Linq;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan;

    /// <summary>
    /// A strategy based on the standard ADR strategy
    /// </summary>
    public class LoRaADRStandardStrategy : ILoRaADRStrategy
    {
        private const int MarginDb = 5;
        private const int MaxTxPowerIndex = 0;
        private const int DefaultDatarate = 0;
        private const int DefaultNbRep = 1;

        /// <summary>
        /// Array to calculate nb Repetion given packet loss
        /// X axis min 5 %, 5-10 %, 10-30%, more than 30%
        /// Y axis currentNbRep 1, 2, 3
        /// </summary>
        private readonly int[,] pktLossToNbRep = new int[4, 3] { { 1, 1, 2 }, { 1, 2, 3 }, { 2, 3, 3 }, { 3, 3, 3 } };

        public LoRaADRResult ComputeResult(LoRaADRTable table, float requiredSnr, int upstreamDataRate, int minTxPower, int maxDr)
        {
            if (table == null || table.Entries.Count < 20)
            {
                Logger.Log("ADR: Not enough frames captured.", Microsoft.Extensions.Logging.LogLevel.Debug);
                return null;
            }

            if (!table.CurrentNbRep.HasValue || !table.CurrentTxPower.HasValue)
            {
                return new LoRaADRResult
                {
                    DataRate = DefaultDatarate,
                    NbRepetition = DefaultNbRep,
                    TxPower = MaxTxPowerIndex
                };
            }

            var newNbRep = this.ComputeNbRepetion(table.Entries[0].FCnt, table.Entries[LoRaADRTable.FrameCountCaptureCount - 1].FCnt, table.CurrentNbRep.GetValueOrDefault());
            (int newTxPowerIndex, int newDatarate) = this.GetPowerAndDRConfiguration(requiredSnr, upstreamDataRate, table.Entries.Max(x => x.Snr), table.CurrentTxPower.GetValueOrDefault(), minTxPower, maxDr);

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

        private (int txPower, int datarate) GetPowerAndDRConfiguration(float requiredSnr, int dataRate, double maxSnr, int currentTxPowerIndex, int minTxPowerIndex, int maxDr)
        {
            double snrMargin = maxSnr - requiredSnr - MarginDb;

            int computedDataRate = dataRate;

            int nStep = (int)snrMargin;

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

        private int ComputeNbRepetion(int first, int last, int currentNbRep)
        {
            double pktLossRate = (float)(last - first - 19) / (last - first);
            if (pktLossRate < 0.05)
            {
                return this.pktLossToNbRep[0, currentNbRep - 1];
            }

            if (pktLossRate >= 0.05 && pktLossRate < 0.10)
            {
                return this.pktLossToNbRep[1, currentNbRep - 1];
            }

            if (pktLossRate >= 0.10 && pktLossRate < 0.30)
            {
                return this.pktLossToNbRep[2, currentNbRep - 1];
            }

            return this.pktLossToNbRep[3, currentNbRep];
        }
    }
}
