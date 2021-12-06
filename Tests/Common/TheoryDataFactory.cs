// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public static class TheoryDataFactory
    {
        public static TheoryData<T> From<T>(IEnumerable<T> data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));

            var result = new TheoryData<T>();
            foreach (var datum in data)
                result.Add(datum);
            return result;
        }

        public static TheoryData<T1, T2, T3, T4, T5, T6> From<T1, T2, T3, T4, T5, T6>(IEnumerable<(T1, T2, T3, T4, T5, T6)> data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));

            var result = new TheoryData<T1, T2, T3, T4, T5, T6>();
            foreach (var (a, b, c, d, e, f) in data)
                result.Add(a, b, c, d, e, f);
            return result;
        }
    }
}
