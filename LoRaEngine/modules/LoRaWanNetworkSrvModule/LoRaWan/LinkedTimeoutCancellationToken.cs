// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Threading;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable (false positive)
    public readonly struct LinkedTimeoutCancellationToken : IEquatable<LinkedTimeoutCancellationToken>, IDisposable
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly CancellationTokenSource? timeoutCts;
        private readonly CancellationTokenSource? linkedCts;

        public CancellationToken Token { get; }

        public LinkedTimeoutCancellationToken(TimeSpan? timeSpan, CancellationToken cancellationToken)
        {
            this.timeoutCts = this.linkedCts = null;

            if (timeSpan is { } ts && ts != Timeout.InfiniteTimeSpan)
            {
                var tempTimeoutCts = this.timeoutCts = new CancellationTokenSource(ts);

                try
                {
                    if (cancellationToken.CanBeCanceled)
                    {
                        this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.timeoutCts.Token, cancellationToken);
                        Token = this.linkedCts.Token;
                        tempTimeoutCts = null;
                    }
                    else
                    {
                        Token = this.timeoutCts.Token;
                    }
                }
                finally
                {
                    tempTimeoutCts?.Dispose();
                }
            }
            else
            {
                Token = cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken.None;
            }
        }

        public void Dispose()
        {
            this.timeoutCts?.Dispose();
            this.linkedCts?.Dispose();
        }

        public bool Equals(LinkedTimeoutCancellationToken other) => Token == other.Token;
        public override bool Equals(object? obj) => obj is LinkedTimeoutCancellationToken other && Equals(other);
        public override int GetHashCode() => Token.GetHashCode();

        public static bool operator ==(LinkedTimeoutCancellationToken left, LinkedTimeoutCancellationToken right) => left.Equals(right);
        public static bool operator !=(LinkedTimeoutCancellationToken left, LinkedTimeoutCancellationToken right) => !(left == right);
    }
}
