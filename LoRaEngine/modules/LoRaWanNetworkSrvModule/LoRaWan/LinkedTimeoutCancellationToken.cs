// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable (false positive)
    public struct LinkedTimeoutCancellationToken : IEquatable<LinkedTimeoutCancellationToken>, IDisposable
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        private readonly CancellationTokenSource? timeoutCts;
        private readonly CancellationTokenSource? linkedCts;

        public CancellationToken Token { get; private set; }

        public LinkedTimeoutCancellationToken(TimeSpan? timeSpan, CancellationToken cancellationToken)
        {
            this.timeoutCts = null;
            this.linkedCts = null;

            if (timeSpan is { } ts && ts != Timeout.InfiniteTimeSpan)
            {
                this.timeoutCts = new CancellationTokenSource(ts);
                var tempTimeoutCts = this.timeoutCts;

                try
                {
                    if (cancellationToken.CanBeCanceled)
                    {
                        this.linkedCts = CancellationTokenSource.CreateLinkedTokenSource(this.timeoutCts.Token, cancellationToken);
                        Token = this.linkedCts.Token;
                    }
                    else
                    {
                        Token = this.timeoutCts.Token;
                    }

                    tempTimeoutCts = null;
                }
                finally
                {
#pragma warning disable CA1508 // Avoid dead conditional code (false positive)
                    if (tempTimeoutCts != null)
#pragma warning restore CA1508 // Avoid dead conditional code
                        tempTimeoutCts.Dispose();
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

        public bool Equals([AllowNull] LinkedTimeoutCancellationToken other) =>
            other is { } o && Token == o.Token;

        public override bool Equals([AllowNull] object obj) =>
            obj is LinkedTimeoutCancellationToken other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Token);

        public static bool operator ==(LinkedTimeoutCancellationToken left, LinkedTimeoutCancellationToken right) =>
            left.Equals(right);

        public static bool operator !=(LinkedTimeoutCancellationToken left, LinkedTimeoutCancellationToken right) =>
            !(left == right);
    }
}
