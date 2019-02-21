// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// Stores ADR tables in memory on the gateway.
    /// This is the default implementation if we have a single gateway environment.
    /// </summary>
    public class LoRaADRInMemoryStore : ILoRaADRStore
    {
        private readonly MemoryCache cache;

        public LoRaADRInMemoryStore()
        {
            // REVIEW: can we set a size limit?
            this.cache = new MemoryCache(new MemoryCacheOptions());
        }

        public Task<LoRaADRTable> GetADRTable(string devEUI)
        {
            lock (this.cache)
            {
                return Task.FromResult(this.cache.Get<LoRaADRTable>(devEUI));
            }
        }

        public Task UpdateADRTable(string devEUI, LoRaADRTable table)
        {
            // void: the reference is up to date already
            return Task.CompletedTask;
        }

        public Task AddTableEntry(LoRaADRTableEntry entry)
        {
            lock (this.cache)
            {
                var table = this.cache.Get<LoRaADRTable>(entry.DevEUI);
                if (table == null)
                {
                    table = new LoRaADRTable();
                    this.cache.Set<LoRaADRTable>(entry.DevEUI, table);
                }

                var existing = table.Entries.FirstOrDefault(itm => itm.FCnt == entry.FCnt);

                if (existing == null)
                {
                    // first for this framecount, simply add it
                    entry.GatewayCount = 1;
                    table.Entries.Add(entry);
                }
                else
                {
                    if (existing.Snr < entry.Snr)
                    {
                        // better update with this entry
                        existing.Snr = entry.Snr;
                        existing.GatewayId = entry.GatewayId;
                    }

                    existing.GatewayCount++;
                }

                if (table.Entries.Count > LoRaADRTable.FrameCountCaptureCount)
                {
                    table.Entries.RemoveAt(0);
                }
            }

            return Task.CompletedTask;
        }
    }
}
