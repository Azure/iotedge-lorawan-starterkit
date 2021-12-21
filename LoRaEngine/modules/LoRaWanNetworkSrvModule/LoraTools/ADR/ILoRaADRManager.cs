// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Threading.Tasks;
    using LoRaWan;

    public interface ILoRaADRManager
    {
        Task StoreADREntryAsync(LoRaADRTableEntry newEntry);

        Task<LoRaADRResult> CalculateADRResultAndAddEntryAsync(string devEUI, string gatewayId, uint fCntUp, uint fCntDown, float requiredSnr, DataRateIndex dataRate, int minTxPower, DataRateIndex maxDr, LoRaADRTableEntry newEntry = null);

        Task<LoRaADRResult> GetLastResultAsync(string devEUI);

        Task<LoRaADRTableEntry> GetLastEntryAsync(string devEUI);

        Task<bool> ResetAsync(string devEUI);
    }
}
