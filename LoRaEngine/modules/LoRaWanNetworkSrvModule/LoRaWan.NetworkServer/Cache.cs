//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
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

        public static void Clear()
        {
            Cache.MemoryCache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        }


        public static void AddRequestToCache(string devAddr, LoraDeviceInfo deviceInfo)
        {

            var concurrentdict = Cache.MemoryCache.GetOrCreate
              (devAddr, entry =>
              {
                  entry.SlidingExpiration = new TimeSpan(1, 0, 0, 0);
                  return new ConcurrentDictionary<string, LoraDeviceInfo>() {};
              });
            concurrentdict.GetOrAdd(deviceInfo.DevEUI, devinf =>
             {
                 return deviceInfo;
             });

               
        }



        public static void TryGetRequestValue(string key, out  ConcurrentDictionary<string,LoraDeviceInfo> loraDeviceInfo)
        {
            Cache.MemoryCache.TryGetValue(key, out object loraDeviceInfoCache);
            loraDeviceInfo = ( ConcurrentDictionary<string,LoraDeviceInfo>)loraDeviceInfoCache;
        }


        public static void AddJoinRequestToCache(string devAddr, LoraDeviceInfo loraDeviceInfo)
        {
            using (var entry = Cache.MemoryCache.CreateEntry(devAddr))
            {
                entry.Value = loraDeviceInfo;
                entry.SlidingExpiration = new TimeSpan(1, 0, 0, 0);
            }
        }

        public static void TryGetJoinRequestValue(string key, out LoraDeviceInfo loraDeviceInfo)
        {
            Cache.MemoryCache.TryGetValue(key, out object loraDeviceInfoCache);

            loraDeviceInfo = (LoraDeviceInfo)loraDeviceInfoCache;
        }
    }
}
