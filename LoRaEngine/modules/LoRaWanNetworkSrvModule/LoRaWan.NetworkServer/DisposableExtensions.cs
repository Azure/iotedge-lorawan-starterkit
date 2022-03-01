// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class DisposableExtensions
    {
        /// <remarks>
        /// In general <see cref="IAsyncDisposable.DisposeAsync"/> implementations are not
        /// expected to throw exceptions. If any of the disposable objects throw exceptions then
        /// the behaviour of this method is undefined.
        /// </remarks>
        public static async ValueTask DisposeAllAsync(this IEnumerable<IAsyncDisposable> disposables, int concurrency)
        {
            ArgumentNullException.ThrowIfNull(disposables, nameof(disposables));
            if (concurrency <= 0) throw new ArgumentOutOfRangeException(nameof(concurrency), concurrency, null);

            var capacity = disposables switch
            {
                ICollection<IAsyncDisposable> collection => collection.Count,
                IReadOnlyCollection<IAsyncDisposable> collection => collection.Count,
                _ => (int?)null,
            };

            if (capacity is 0) // disposables collection is empty so bail out early
                return;

            using var semaphore = new SemaphoreSlim(concurrency);

            var tasks = capacity is { } someCapacity ? new List<Task>(someCapacity) : new List<Task>();
            tasks.AddRange(disposables.Select(DisposeAsync));

            // NOTE! "IAsyncDisposable.DisposeAsync" implementations are not meant to throw
            // and cannot be canceled therefore it is expected that all of the tasks will
            // always succeed.

            await Task.WhenAll(tasks).ConfigureAwait(false);

            async Task DisposeAsync(IAsyncDisposable device)
            {
                try
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    await device.DisposeAsync();
                }
                finally
                {
                    _ = semaphore.Release();
                }
            }
        }
    }
}
