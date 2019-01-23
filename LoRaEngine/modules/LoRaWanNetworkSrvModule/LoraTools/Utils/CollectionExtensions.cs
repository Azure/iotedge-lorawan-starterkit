// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Utils
{
    using System;
    using System.Collections.Generic;

    public static class CollectionExtensions
    {
        public static bool TryGetValueCaseInsensitive(this IDictionary<string, string> dict, string key, out string value)
        {
            value = null;
            if (dict == null)
            {
                return false;
            }

            foreach (var kv in dict)
            {
                if (kv.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    value = kv.Value;
                    return true;
                }
            }

            return false;
        }
    }
}