// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Used for setting up/mocking classes for testing, if intermediate disposables need to be disposed at the end of the test.
    /// For example, when creating an instance of MessageDispatcher for testing, we need to create many intermediate
    /// disposable instances to be passed as arguments (LoRaDevice, etc), which we don't need by their value, but which still need to be disposed of.
    /// With this class we execute an arbitrary dispose action after we use some value.
    /// </summary>
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

    public sealed class AsyncDisposableValue<T> : IAsyncDisposable
    {
        private readonly Func<ValueTask> dispose;

        public AsyncDisposableValue(T value, IAsyncDisposable disposable)
            : this(value, () => disposable.DisposeAsync())
        { }

        public AsyncDisposableValue(T value, Func<ValueTask> dispose) =>
            (Value, this.dispose) = (value, dispose);

        public T Value { get; }

        public ValueTask DisposeAsync() => dispose();
    }
}
