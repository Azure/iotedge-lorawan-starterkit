﻿// Copyright (c) Microsoft. All rights reserved.
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

        public LoRaADRManagerBase(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider)
        {
            this.store = store;
            this.strategyProvider = strategyProvider;
        }

        protected virtual Task<bool> TryUpdateStateAsync(LoRaADRResult loRaADRResult)
        {
            return Task.FromResult<bool>(true);
        }

        public virtual async Task StoreADREntryAsync(LoRaADRTableEntry newEntry)
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

        public virtual async Task<LoRaADRResult> CalculateADRResultAndAddEntryAsync(string devEUI, string gatewayId, int fCntUp, int fCntDown, float requiredSnr, int upstreamDataRate, int minTxPower, int maxDr, LoRaADRTableEntry newEntry = null)
        {
            var table = newEntry != null
                        ? await this.store.AddTableEntry(newEntry)
                        : await this.store.GetADRTable(devEUI);

            var currentStrategy = this.strategyProvider.GetStrategy();

            var result = currentStrategy.ComputeResult(table, requiredSnr, upstreamDataRate, minTxPower, maxDr);

            if (result == null)
            {
                // In this case we want to reset the device to default values as we have null values
                if (table == null
                    || !table.CurrentNbRep.HasValue
                    || !table.CurrentTxPower.HasValue
                    || ((table.Entries.Count < currentStrategy.MinimumNumberOfResult) && ((fCntUp - 10) > table.Entries.Count)))
                {
                    result = this.ReturnDefaultValues(upstreamDataRate, currentStrategy.DefaultNbRep, currentStrategy.DefaultTxPower);
                }
                else
                {
                    result = await this.GetLastResultAsync(devEUI) ?? new LoRaADRResult();
                    result.NumberOfFrames = table.Entries.Count;
                    return result;
                }
            }

            var nextFcntDown = await this.NextFCntDown(devEUI, gatewayId, fCntUp, fCntDown);
            result.CanConfirmToDevice = nextFcntDown > 0;

            if (result.CanConfirmToDevice)
            {
                table.CurrentNbRep = result.NbRepetition;
                table.CurrentTxPower = result.TxPower;
                await this.store.UpdateADRTable(devEUI, table);
                await this.TryUpdateStateAsync(result);
                result.FCntDown = nextFcntDown;
            }

            result.NumberOfFrames = table.Entries.Count;
            Logger.Log(devEUI, $"calculated ADR: CanConfirmToDevice: {result.CanConfirmToDevice}, TxPower: {result.TxPower}, DataRate: {result.DataRate}", LogLevel.Debug);
            return result;
        }

        public virtual Task<int> NextFCntDown(string devEUI, string gatewayId, int clientFCntUp, int clientFCntDown)
        {
            return Task.FromResult<int>(-1);
        }

        public virtual async Task<LoRaADRResult> GetLastResultAsync(string devEUI)
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

        public virtual async Task<LoRaADRTableEntry> GetLastEntryAsync(string devEUI)
        {
            var table = await this.store.GetADRTable(devEUI);
            return table != null && table.Entries.Count > 0 ? table.Entries[table.Entries.Count - 1] : null;
        }

        public virtual async Task<bool> ResetAsync(string devEUI)
        {
            return await this.store.Reset(devEUI);
        }

        private LoRaADRResult ReturnDefaultValues(int upstreamDataRate, int defaultNbRep, int maxTxPowerIndex)
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
