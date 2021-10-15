// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Utils
{
    using System;
    using System.Collections.Generic;

    public static class CollectionExtensions
    {
        public static T[] RangeSubset<T>(this T[] array, int startIndex, int length)
        {
            var subset = new T[length];
            Array.Copy(array, startIndex, subset, 0, length);
            return subset;
        }

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
