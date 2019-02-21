// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;

    /// <summary>
    /// A strategy based on the standard ADR strategy
    /// </summary>
    public class LoRaADRStandardStrategy : ILoRaADRStrategy
    {
        private const int MarginDb = 5;
        private const int MaxDR = 5;
        private const int MaxTxPowerIndex = 0;

        /// <summary>
        /// Array to calculate nb Repetion given packet loss
        /// X axis min 5 %, 5-10 %, 10-30%, more than 30%
        /// Y axis currentNbRep 1, 2, 3
        /// </summary>
        private readonly int[,] pktLossToNbRep = new int[4, 3] { { 1, 1, 2 }, { 1, 2, 3 }, { 2, 3, 3 }, { 3, 3, 3 } };

        public (int txPower, int datarate) GetPowerAndDRConfiguration(float requiredSnr, int dataRate, double maxSnr, int currentTxPowerIndex, int minTxPowerIndex)
        {
            double snrMargin = maxSnr - requiredSnr - MarginDb;

            int computedDatarate = dataRate;

            int nStep = (int)snrMargin;

            while (nStep != 0)
            {
                if (nStep > 0)
                {
                    if (computedDatarate < MaxDR)
                    {
                        computedDatarate++;
                    }
                    else if (currentTxPowerIndex < minTxPowerIndex)
                    {
                        currentTxPowerIndex++;
                    }

                    nStep--;
                    if (currentTxPowerIndex >= minTxPowerIndex)
                        return (currentTxPowerIndex, computedDatarate);
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
                        return (currentTxPowerIndex, computedDatarate);
                    }
                }
            }

            return (currentTxPowerIndex, computedDatarate);
        }

        public int ComputeNbRepetion(int first, int last, int currentNbRep)
        {
            double pktLossRate = (last - first - 20) / (last - first);
            if (pktLossRate < 0.05)
            {
                return this.pktLossToNbRep[0, currentNbRep];
            }

            if (pktLossRate >= 0.05 && pktLossRate < 0.10)
            {
                return this.pktLossToNbRep[1, currentNbRep];
            }

            if (pktLossRate >= 0.10 && pktLossRate < 0.30)
            {
                return this.pktLossToNbRep[2, currentNbRep];
            }

            return this.pktLossToNbRep[3, currentNbRep];
        }
    }
}
