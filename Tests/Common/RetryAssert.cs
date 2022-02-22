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
            RetryAsync(() =>
            {
                try
                {
                    Assert.Contains(enumerable, predicate);
                    return ((int?)0, true);
                }
                catch (ContainsException)
                {
                    return (default, false);
                }
            }, maxAttempts, interval);

        public static Task<T> SingleAsync<T>(IEnumerable<T> enumerable, int maxAttempts = 3, TimeSpan? interval = null) =>
            RetryAsync(() =>
            {
                try
                {
                    return (Assert.Single(enumerable), true);
                }
                catch (SingleException)
                {
                    return (default, false);
                }
            }, maxAttempts, interval);

        private static async Task<T> RetryAsync<T>(Func<(T?, bool)> assert, int maxAttempts, TimeSpan? interval)
        {
            var effectiveInterval = interval ?? DefaultInterval;

            var i = 0;
            while (true)
            {
                if (assert() is (var result, true))
                    return result ?? throw new InvalidOperationException("Result of the assertion was null.");

                if (++i == maxAttempts)
                    throw new InvalidOperationException("Maximum number of retries exceeded. Assertion failed.");

                await Task.Delay(effectiveInterval);
            }
        }
    }
}
