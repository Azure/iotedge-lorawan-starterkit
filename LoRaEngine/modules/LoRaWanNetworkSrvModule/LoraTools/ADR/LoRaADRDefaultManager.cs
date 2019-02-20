// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan;

    public class LoRaADRDefaultManager : ILoRaADRManager
    {
        private readonly ILoRaADRStore store;
        private readonly ILoRaADRStrategyProvider strategyProvider;

        public LoRaADRDefaultManager(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider)
        {
            this.store = store;
            this.strategyProvider = strategyProvider;
        }

        public async Task StoreADREntry(LoRaADRTableEntry newEntry)
        {
            if (newEntry == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(newEntry.DevEUI) ||
               string.IsNullOrEmpty(newEntry.GatewayId))
            {
                throw new ArgumentException("Missing DevEUI or GatewayId");
            }

            await this.store.AddTableEntry(newEntry);
        }

        public async Task<LoRaADRResult> CalculateADRResult(string devEUI, float requiredSnr, int dataRate, LoRaADRTableEntry newEntry = null)
        {
            if (newEntry != null)
            {
                await this.store.AddTableEntry(newEntry);
            }

            var table = await this.store.GetADRTable(devEUI);
            if (table == null || !table.IsComplete)
            {
                Logger.Log(devEUI, "Can't calculate ADR. Not enough frame captured.", Microsoft.Extensions.Logging.LogLevel.Debug);
                return null;
            }

            // calculate ADR answer
            if (table.CurrentNbRep == null)
            {
                throw new ADRException("Missing values for currentTxPower or Current Nb Rep, aborting ADR calculation");
            }

            var newNbRep = this.strategyProvider.GetStrategy().ComputeNbRepetion(table.Entries[0].FCnt, table.Entries[LoRaADRTable.FrameCountCaptureCount - 1].FCnt, (int)table.CurrentNbRep);
            (int newTxPowerIndex, int newDatarate) = this.strategyProvider.GetStrategy().GetPowerAndDRConfiguration(requiredSnr, dataRate, table.Entries.Max(x => x.Snr), (int)table.CurrentTxPower);

            // checking if the new values are different from the old ones otherwise we don't do any adr change$
            if (newNbRep != table.CurrentNbRep || newTxPowerIndex != table.CurrentTxPower || newDatarate != dataRate)
            {
                LoRaADRResult result = new LoRaADRResult()
                {
                    DataRate = newDatarate,
                    NbRepetition = newNbRep,
                    TxPower = newTxPowerIndex
                };

                return result;
            }

            return null;
        }
    }
}
