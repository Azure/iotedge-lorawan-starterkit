// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Threading.Tasks;
    using LoRaWan;

    public static class LoRaADRManager
    {
        private static ILoRaADRStore store;

        public static void Initialize(ILoRaADRStore store)
        {
            LoRaADRManager.store = store;
        }

        public static async Task<LoRaADRResult> CalculateADRResult(string devEUI, LoRaADRTableEntry newEntry = null)
        {
            if (newEntry != null)
            {
                await store.AddTableEntry(newEntry);
            }

            var table = await store.GetADRTable(devEUI);
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
