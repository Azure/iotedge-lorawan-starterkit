// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan;
    using Microsoft.Extensions.Caching.Memory;

    /// <summary>
    /// Stores ADR tables in memory on the gateway.
    /// This is the default implementation if we have a single gateway environment.
    /// </summary>
    public sealed class LoRaADRInMemoryStore : LoRaADRStoreBase, ILoRaADRStore, IDisposable
    {
        private readonly MemoryCache cache;

        public LoRaADRInMemoryStore()
        {
            // REVIEW: can we set a size limit?
            this.cache = new MemoryCache(new MemoryCacheOptions());
        }

        public Task<LoRaADRTable> GetADRTable(DevEui devEUI)
        {
            lock (this.cache)
            {
                return Task.FromResult(this.cache.Get<LoRaADRTable>(devEUI));
            }
        }

        public Task UpdateADRTable(DevEui devEUI, LoRaADRTable table)
        {
            // void: the reference is up to date already
            return Task.CompletedTask;
        }

        public Task<LoRaADRTable> AddTableEntry(LoRaADRTableEntry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            lock (this.cache)
            {
                var table = this.cache.GetOrCreate(entry.DevEUI, (cacheEntry) => new LoRaADRTable());
                AddEntryToTable(table, entry);
                return Task.FromResult(table);
            }
        }

        public Task<bool> Reset(DevEui devEUI)
        {
            lock (this.cache)
            {
                this.cache.Remove(devEUI);
            }

            return Task.FromResult<bool>(true);
        }

        public void Dispose() => this.cache.Dispose();
    }
}
