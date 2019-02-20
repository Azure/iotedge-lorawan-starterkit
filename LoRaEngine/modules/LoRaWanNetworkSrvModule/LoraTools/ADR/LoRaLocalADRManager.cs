// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan;

    internal class LoRaLocalADRManager : ILoRaADRManager
    {
        private readonly ILoRaADRStore store;
        private readonly ILoRaADRStrategyProvider strategyProvider;

        public LoRaLocalADRManager(ILoRaADRStore store, ILoRaADRStrategyProvider strategyProvider)
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

        public async Task<LoRaADRResult> CalculateADRResult(string devEUI)
        {
            var table = await this.store.GetADRTable(devEUI);
            if (table == null || !table.IsComplete)
            {
                Logger.Log(devEUI, "Can't calculate ADR. Not enough framew captured.", Microsoft.Extensions.Logging.LogLevel.Debug);
                return null;
            }

            // calculate
            return null;
        }
    }
}
