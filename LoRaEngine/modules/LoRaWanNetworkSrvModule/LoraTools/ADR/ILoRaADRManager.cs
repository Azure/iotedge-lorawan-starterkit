// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Threading.Tasks;

    public interface ILoRaADRManager
    {
        Task StoreADREntry(LoRaADRTableEntry newEntry);

        Task<LoRaADRResult> CalculateADRResult(string devEUI);
    }
}
