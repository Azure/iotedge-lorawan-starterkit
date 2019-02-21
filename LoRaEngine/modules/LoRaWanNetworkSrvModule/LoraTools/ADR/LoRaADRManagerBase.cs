// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using System.Threading.Tasks;

    public class LoRaADRManagerBase : ILoRaADRManager
    {
        private readonly ILoRaADRStore store;
        private readonly ILoRaADRStrategyProvider strategyProvider;

        public LoRaADRManagerBase(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider)
        {
            this.store = store;
            this.strategyProvider = strategyProvider;
        }

        protected virtual Task<bool> TryUpdateState(LoRaADRResult loRaADRResult)
        {
            return Task.FromResult<bool>(true);
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

        public async Task<LoRaADRResult> CalculateADRResultAndAddEntry(string devEUI, string gatewayId, int fCntUp, int fCntDown, float requiredSnr, int upstreamDataRate, int minTxPower, LoRaADRTableEntry newEntry = null)
        {
            if (newEntry != null)
            {
                await this.store.AddTableEntry(newEntry);
            }

            var table = await this.store.GetADRTable(devEUI);
            var result = this.strategyProvider.GetStrategy().ComputeResult(table, requiredSnr, upstreamDataRate, minTxPower);

            if (result != null)
            {
                var nextFcntDown = await this.NextFCntDown(devEUI, gatewayId, fCntUp, fCntDown);
                result.CanConfirmToDevice = nextFcntDown > 0;

                if (result.CanConfirmToDevice)
                {
                    table.CurrentNbRep = result.NbRepetition;
                    table.CurrentTxPower = result.TxPower;
                    await this.store.UpdateADRTable(devEUI, table);
                    await this.TryUpdateState(result);
                    result.FCntDown = nextFcntDown;
                }
            }

            return result;
        }

        public virtual Task<int> NextFCntDown(string devEUI, string gatewayId, int clientFCntUp, int clientFCntDown)
        {
            return Task.FromResult<int>(-1);
        }

        public async Task<LoRaADRResult> GetLastResult(string devEUI)
        {
            var table = await this.store.GetADRTable(devEUI);
            return new LoRaADRResult
            {
                NbRepetition = table.CurrentNbRep,
                TxPower = table.CurrentTxPower
            };
        }
    }
}
