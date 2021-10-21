// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Runtime.CompilerServices;

    internal static class Operators<T>
    {
        private static Func<T, T, bool> LogicalBinary([CallerMemberName] string name = null) =>
            (Func<T, T, bool>)Delegate.CreateDelegate(typeof(Func<T, T, bool>), typeof(T), $"op_{name}");

        private static Func<T, T, bool> equality;
        private static Func<T, T, bool> inequality;

        public static Func<T, T, bool> Equality => equality ??= LogicalBinary();
        public static Func<T, T, bool> Inequality => inequality ??= LogicalBinary();
    }
}
