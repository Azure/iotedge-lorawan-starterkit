// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ILoRaDeviceCacheStore
    {
        /// <summary>
        /// Tries to acquire a lock for a specific key.
        /// </summary>
        /// <param name="key">The lock key to try to acquire</param>
        /// <param name="owner">The lock owner</param>
        /// <param name="expiration">expiration timestamp, this specifies how long the lock will be held before automatically releasing</param>
        /// <param name="block">true to retry getting the lock in contention cases, false to try once and return the result</param>
        /// <returns>true if the lock was acquired otherwise false.</returns>
        Task<bool> LockTakeAsync(string key, string owner, TimeSpan expiration, bool block = true);

        string StringGet(string key);

        T GetObject<T>(string key)
            where T : class;

        bool StringSet(string key, string value, TimeSpan? expiration, bool onlyIfNotExists = false);

        bool ObjectSet<T>(string key, T value, TimeSpan? expiration, bool onlyIfNotExists = false)
            where T : class;

        bool KeyDelete(string key);

        bool KeyExists(string key);

        bool LockRelease(string key, string value);

        long ListAdd(string key, string value, TimeSpan? expiration = null);

        IReadOnlyList<string> ListGet(string key);
    }
}
