// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;

    public static class CancellationTokenExtensions
    {
        /// <summary>
        /// Links an existing cancellation token with another based on a time-out.
        /// </summary>
        /// <remarks>
        /// Returns a cancellation token paired with an optional (cancellation token source)
        /// disposable.
        /// </remarks>
        /// <remarks>
        /// If <paramref name="timeout"/> is <c>null</c> or <see cref="Timeout.InfiniteTimeSpan"/>
        /// then the returned pair contains <paramref name="cancellationToken"/>.
        /// </remarks>
        public static DisposableCancellationTokenPair LinkWithTimeout(this CancellationToken cancellationToken, TimeSpan? timeout)
        {
            if (timeout is { } someTimeout && someTimeout != Timeout.InfiniteTimeSpan)
            {
                if (someTimeout.Ticks < 0)
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, null);

#pragma warning disable CA2000 // Dispose objects before losing scope (false positive)
                var timeoutCts = new CancellationTokenSource(someTimeout);
#pragma warning restore CA2000 // Dispose objects before losing scope
                if (!cancellationToken.CanBeCanceled)
                    return new DisposableCancellationTokenPair(timeoutCts, timeoutCts.Token);

                try
                {
#pragma warning disable CA2000 // Dispose objects before losing scope (false positive)
                    var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
#pragma warning restore CA2000 // Dispose objects before losing scope
                    return new DisposableCancellationTokenPair(linkedCts, linkedCts.Token);
                }
                catch
                {
                    timeoutCts.Dispose();
                    throw;
                }
            }
            else
            {
                return new DisposableCancellationTokenPair(null, cancellationToken);
            }
        }
    }
}
