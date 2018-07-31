using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer
{
    public static class Cache
    {

        private static IMemoryCache MemoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());


        public static void AddToCache(string devAddr, LoraDeviceInfo loraDeviceInfo)
        {
            using (var entry = Cache.MemoryCache.CreateEntry(devAddr))
            {
                entry.Value = loraDeviceInfo;
                entry.SlidingExpiration = new TimeSpan(1, 0, 0, 0);
            }
        }
        
        public static void TryGetValue(string key, out LoraDeviceInfo loraDeviceInfo)
        {
            Cache.MemoryCache.TryGetValue(key, out object loraDeviceInfoCache);

            loraDeviceInfo = (LoraDeviceInfo)loraDeviceInfoCache;

           
        } 
    }
}
