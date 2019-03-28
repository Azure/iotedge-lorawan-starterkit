// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Collections.Generic;

    public interface ILoRaDeviceCacheStore
    {
        bool LockTake(string key, string value, TimeSpan timeout);

        string StringGet(string key);

        bool StringSet(string key, string value, TimeSpan? expiry, bool onlyIfNotExists = false);

        bool KeyDelete(string key);

        bool KeyExists(string key);

        bool LockRelease(string key, string value);

        long ListAdd(string key, string value, TimeSpan? expiry = null);

        IReadOnlyList<string> ListGet(string key);
    }
}
