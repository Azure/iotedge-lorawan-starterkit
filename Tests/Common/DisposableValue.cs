// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;

    public sealed class DisposableValue<T> : IDisposable
    {
        private readonly IDisposable disposable;

        public DisposableValue(T value, IDisposable disposable) =>
            (Value, this.disposable) = (value, disposable);

        public T Value { get; }

        public void Dispose() => this.disposable.Dispose();
    }
}
