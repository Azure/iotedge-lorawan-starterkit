// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Threading.Tasks;

    /// <summary>
    /// Interface to implement to store ADR tables
    /// </summary>
    public interface ILoRaADRStore
    {
        Task AddTableEntry(LoRaADRTableEntry entry);

        Task<LoRaADRTable> GetADRTable(string devEUI);
    }
}
