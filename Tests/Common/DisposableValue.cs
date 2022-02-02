// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;

    public sealed class DisposableValue<T> : IDisposable
    {
        private readonly Action dispose;

        public DisposableValue(T value, IDisposable disposable)
            : this(value, () => disposable.Dispose())
        { }

        public DisposableValue(T value, Action dispose) =>
            (Value, this.dispose) = (value, dispose);

        public T Value { get; }

        public void Dispose() => this.dispose();
    }
}
