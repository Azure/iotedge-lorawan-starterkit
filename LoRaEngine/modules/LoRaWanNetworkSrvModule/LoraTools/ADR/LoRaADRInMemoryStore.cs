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
    public class LoRaADRInMemoryStore : LoRaADRStoreBase, ILoRaADRStore
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

        public Task<LoRaADRTable> AddTableEntry(LoRaADRTableEntry entry)
        {
            lock (this.cache)
            {
                var table = this.cache.GetOrCreate<LoRaADRTable>(entry.DevEUI, (cacheEntry) => new LoRaADRTable());
                AddEntryToTable(table, entry);
                return Task.FromResult<LoRaADRTable>(table);
            }
        }

        public Task<bool> Reset(string devEUI)
        {
            lock (this.cache)
            {
                this.cache.Remove(devEUI);
            }

            return Task.FromResult<bool>(true);
        }
    }
}
