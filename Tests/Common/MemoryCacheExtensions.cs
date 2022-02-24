// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Memory;

    public static class MemoryCacheExtensions
    {
        /// <summary>
        /// Waits for at most 30s for the eviction of an item from the memory cache.
        /// </summary>
        public static async Task WaitForEvictionAsync(this IMemoryCache memoryCache, object key, CancellationToken cancellationToken)
        {
            if (memoryCache is null) throw new ArgumentNullException(nameof(memoryCache));

            var waitInterval = TimeSpan.FromSeconds(2);
            var timeout = TimeSpan.FromSeconds(30);
            using var timeoutTokenSource = new CancellationTokenSource(timeout);
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);

            while (true)
            {
                if (!memoryCache.TryGetValue(key, out var _))
                    return;

                try
                {
                    await Task.Delay(waitInterval, linkedTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new OperationCanceledException($"Item with key '{key}' was not evicted after {timeout.TotalSeconds} seconds.");
                }
            }
        }
    }
}
