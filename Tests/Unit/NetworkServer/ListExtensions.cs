// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System.Collections;
    using System.Collections.Generic;

    internal static class ListExtensions
    {
        public static IReadOnlyCollection<T> WrapInReadOnlyCollection<T>(this IList<T> list) => new ReadOnlyCollection<T>(list);

        internal sealed class ReadOnlyCollection<T> : IReadOnlyCollection<T>
        {
            private readonly IList<T> list;

            public ReadOnlyCollection(IList<T> list) => this.list = list;
            public int Count => this.list.Count;
            public IEnumerator<T> GetEnumerator() => this.list.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)this.list).GetEnumerator();
        }
    }
}
