// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;

    public static class CollectionExtensions
    {
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            if (range is null) throw new ArgumentNullException(nameof(range));

            foreach (var item in range)
                collection.Add(item);
        }

        public static void ResetTo<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            if (range is null) throw new ArgumentNullException(nameof(range));

            collection.Clear();
            collection.AddRange(range);
        }
    }
}
