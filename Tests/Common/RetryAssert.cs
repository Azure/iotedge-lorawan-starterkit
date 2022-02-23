// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Sdk;

    public static class RetryAssert
    {
        private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(5);

        public static Task ContainsAsync<T>(T expected, IEnumerable<T> enumerable, int maxAttempts = 3, TimeSpan? interval = null) =>
            ContainsAsync(el => expected?.Equals(el) ?? throw new ArgumentNullException(nameof(expected)), enumerable, maxAttempts, interval);

        public static Task ContainsAsync<T>(Predicate<T> predicate, IEnumerable<T> enumerable, int maxAttempts = 3, TimeSpan? interval = null) =>
            RetryAssertAsync<int>(() =>
            {
                try
                {
                    Assert.Contains(enumerable, predicate);
                    return (0, null);
                }
                catch (ContainsException ex)
                {
                    return (default, ex);
                }
            }, maxAttempts, interval);

        public static Task<T> SingleAsync<T>(IEnumerable<T> enumerable, int maxAttempts = 3, TimeSpan? interval = null) =>
            RetryAssertAsync<T>(() =>
            {
                try
                {
                    return (Assert.Single(enumerable), null);
                }
                catch (SingleException ex)
                {
                    return (default, ex);
                }
            }, maxAttempts, interval);

        private static async Task<T> RetryAssertAsync<T>(Func<(T?, Exception?)> assert, int maxAttempts, TimeSpan? interval)
        {
            var effectiveInterval = interval ?? DefaultInterval;

            var i = 0;
            while (true)
            {
                var (result, ex) = assert();

                if (ex is { } someException)
                {
                    if (++i == maxAttempts)
                        throw new InvalidOperationException("Maximum number of retries exceeded.", ex);
                }
                else
                {
                    return result ?? throw new InvalidOperationException("Result of the assertion may not be null.");
                }

                await Task.Delay(effectiveInterval);
            }
        }
    }
}
