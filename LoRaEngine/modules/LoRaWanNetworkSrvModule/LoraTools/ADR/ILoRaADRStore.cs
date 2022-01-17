// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Threading.Tasks;
    using LoRaWan;

    /// <summary>
    /// Interface to implement to store ADR tables.
    /// </summary>
    public interface ILoRaADRStore
    {
        Task<LoRaADRTable> AddTableEntry(LoRaADRTableEntry entry);

        Task UpdateADRTable(DevEui devEUI, LoRaADRTable table);

        Task<LoRaADRTable> GetADRTable(DevEui devEUI);

        Task<bool> Reset(DevEui devEUI);
    }
}
