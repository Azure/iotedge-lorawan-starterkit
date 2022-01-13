// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;

    public readonly record struct DisposableCancellationTokenPair : IDisposable
    {
        private readonly IDisposable? disposable;
        private readonly CancellationToken cancellationToken;

        public DisposableCancellationTokenPair(IDisposable? disposable, CancellationToken cancellationToken) =>
            (this.disposable, this.cancellationToken) = (disposable, cancellationToken);

        public void Dispose() => this.disposable?.Dispose();

        public static implicit operator CancellationToken(DisposableCancellationTokenPair tokenPair) => tokenPair.cancellationToken;
    }
}
