// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public class LoRaADRManagerBase : ILoRaADRManager
    {
        private readonly ILoRaADRStore store;
        private readonly ILoRaADRStrategyProvider strategyProvider;
        private readonly ILogger<LoRaADRManagerBase> logger;

        public LoRaADRManagerBase(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider, ILogger<LoRaADRManagerBase> logger)
        {
            this.store = store;
            this.strategyProvider = strategyProvider;
            this.logger = logger;
        }

        protected virtual void UpdateState(LoRaADRResult loRaADRResult)
        {
        }

        public virtual async Task StoreADREntryAsync(LoRaADRTableEntry newEntry)
        {
            if (newEntry == null)
            {
                return;
            }

            if (!newEntry.DevEUI.IsValid || string.IsNullOrEmpty(newEntry.GatewayId))
            {
                throw new ArgumentException("Missing Gateway ID or invalid DevEUI");
            }

            _ = await this.store.AddTableEntry(newEntry);
        }

        public virtual async Task<LoRaADRResult> CalculateADRResultAndAddEntryAsync(DevEui devEUI, string gatewayId, uint fCntUp, uint fCntDown, float requiredSnr, DataRateIndex dataRate, int minTxPower, DataRateIndex maxDr, LoRaADRTableEntry newEntry = null)
        {
            var table = newEntry != null
                        ? await this.store.AddTableEntry(newEntry)
                        : await this.store.GetADRTable(devEUI);

            var currentStrategy = this.strategyProvider.GetStrategy();

            var result = currentStrategy.ComputeResult(table, requiredSnr, dataRate, minTxPower, maxDr);

            if (result == null)
            {
                // In this case we want to reset the device to default values as we have null values
                if (table == null
                    || !table.CurrentNbRep.HasValue
                    || !table.CurrentTxPower.HasValue
                    || fCntUp > currentStrategy.MinimumNumberOfResult)
                {
                    result = ReturnDefaultValues(dataRate, currentStrategy.DefaultNbRep, currentStrategy.DefaultTxPower);
                }
                else
                {
                    result = await GetLastResultAsync(devEUI) ?? new LoRaADRResult();
                    result.NumberOfFrames = table.Entries.Count;
                    return result;
                }
            }

            var nextFcntDown = await NextFCntDown(devEUI, gatewayId, fCntUp, fCntDown);
            result.CanConfirmToDevice = nextFcntDown > 0;

            if (result.CanConfirmToDevice)
            {
                table ??= new LoRaADRTable();

                table.CurrentNbRep = result.NbRepetition;
                table.CurrentTxPower = result.TxPower;
                await this.store.UpdateADRTable(devEUI, table);
                UpdateState(result);
                result.FCntDown = nextFcntDown;
            }

            result.NumberOfFrames = table.Entries.Count;
            this.logger.LogDebug($"calculated ADR: CanConfirmToDevice: {result.CanConfirmToDevice}, TxPower: {result.TxPower}, DataRate: {result.DataRate}");
            return result;
        }

        public virtual Task<uint> NextFCntDown(DevEui devEUI, string gatewayId, uint clientFCntUp, uint clientFCntDown)
        {
            return Task.FromResult<uint>(0);
        }

        public virtual async Task<LoRaADRResult> GetLastResultAsync(DevEui devEUI)
        {
            var table = await this.store.GetADRTable(devEUI);

            return table != null
                ? new LoRaADRResult
                {
                    NbRepetition = table.CurrentNbRep,
                    TxPower = table.CurrentTxPower,
                    NumberOfFrames = table.Entries.Count
                }
                : null;
        }

        public virtual async Task<LoRaADRTableEntry> GetLastEntryAsync(DevEui devEUI)
        {
            var table = await this.store.GetADRTable(devEUI);
            return table != null && table.Entries.Count > 0 ? table.Entries[table.Entries.Count - 1] : null;
        }

        public virtual async Task<bool> ResetAsync(DevEui devEUI)
        {
            return await this.store.Reset(devEUI);
        }

        private static LoRaADRResult ReturnDefaultValues(DataRateIndex upstreamDataRate, int defaultNbRep, int maxTxPowerIndex)
        {
            return new LoRaADRResult
            {
                DataRate = upstreamDataRate,
                NbRepetition = defaultNbRep,
                TxPower = maxTxPowerIndex
            };
        }
    }
}
