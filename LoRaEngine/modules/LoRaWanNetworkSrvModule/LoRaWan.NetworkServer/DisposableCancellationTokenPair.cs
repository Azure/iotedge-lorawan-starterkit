// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;

    public readonly struct DisposableCancellationTokenPair :
        IEquatable<DisposableCancellationTokenPair>,
        IDisposable
    {
        private readonly IDisposable? disposable;
        private readonly CancellationToken cancellationToken;

        public DisposableCancellationTokenPair(IDisposable? disposable, CancellationToken cancellationToken) =>
            (this.disposable, this.cancellationToken) = (disposable, cancellationToken);

        public void Dispose() => this.disposable?.Dispose();

        public bool Equals(DisposableCancellationTokenPair other) =>
            Equals(this.disposable, other.disposable) && this.cancellationToken.Equals(other.cancellationToken);

        public override bool Equals(object? obj) =>
            obj is DisposableCancellationTokenPair other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(this.disposable, this.cancellationToken);

        public static bool operator ==(DisposableCancellationTokenPair left, DisposableCancellationTokenPair right) => left.Equals(right);
        public static bool operator !=(DisposableCancellationTokenPair left, DisposableCancellationTokenPair right) => !left.Equals(right);

        public static implicit operator CancellationToken(DisposableCancellationTokenPair tokenPair) => tokenPair.cancellationToken;
    }
}
