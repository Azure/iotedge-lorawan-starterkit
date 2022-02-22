// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class AsyncDisposable : IAsyncDisposable
    {
        public static readonly IAsyncDisposable Nop = new AsyncDisposable(_ => ValueTask.CompletedTask);

        private Func<CancellationToken, ValueTask>? handler;

        public AsyncDisposable(Func<CancellationToken, ValueTask> handler) => this.handler = handler;

        public CancellationToken CancellationToken { get; set; }

        public ValueTask DisposeAsync() =>
            Interlocked.CompareExchange(ref this.handler, null, this.handler) is { } handler
                ? handler(CancellationToken)
                : ValueTask.CompletedTask;
    }
}
