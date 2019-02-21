// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;

    public interface ILoRaADRManager
    {
        Task StoreADREntry(LoRaADRTableEntry newEntry);

        Task<LoRaADRResult> CalculateADRResultAndAddEntry(string devEUI, string gatewayId, int fCntUp, int fCntDown, float requiredSnr, int dataRate, int minTxPower, LoRaADRTableEntry newEntry = null);

        Task<LoRaADRResult> GetLastResult(string devEUI);
    }
}
