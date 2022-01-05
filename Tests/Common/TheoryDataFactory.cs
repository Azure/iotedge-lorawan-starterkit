// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xunit;

    public static class TheoryDataFactory
    {
        public static TheoryData<T> From<T>(params T[] data) =>
            From(data.AsEnumerable());

        public static TheoryData<T> From<T>(IEnumerable<T> data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));

            var result = new TheoryData<T>();
            foreach (var datum in data)
                result.Add(datum);
            return result;
        }

        public static TheoryData<T1, T2> From<T1, T2>(params (T1, T2)[] data) =>
            From(data.AsEnumerable());

        public static TheoryData<T1, T2> From<T1, T2>(IEnumerable<(T1, T2)> data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));

            var result = new TheoryData<T1, T2>();
            foreach (var (a, b) in data)
                result.Add(a, b);
            return result;
        }

        public static TheoryData<T1, T2, T3> From<T1, T2, T3>(IEnumerable<(T1, T2, T3)> data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));

            var result = new TheoryData<T1, T2, T3>();
            foreach (var (a, b, c) in data)
                result.Add(a, b, c);
            return result;
        }

        public static TheoryData<T1, T2, T3, T4> From<T1, T2, T3, T4>(IEnumerable<(T1, T2, T3, T4)> data)
        {
            if (data is null) throw new ArgumentNullException(nameof(data));

            var result = new TheoryData<T1, T2, T3, T4>();
            foreach (var (a, b, c, d) in data)
                result.Add(a, b, c, d);
            return result;
        }
    }
}
