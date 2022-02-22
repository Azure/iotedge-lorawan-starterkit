// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Xunit;
    using Xunit.Sdk;

    public static class RetryAssert
    {
        public static Task ContainsAsync<T>(T expected, IEnumerable<T> collection, int maxAttempts = 3, TimeSpan? interval = null) =>
            ContainsAsync(el => expected.Equals(el), collection, maxAttempts, interval);

        public static async Task ContainsAsync<T>(Predicate<T> predicate, IEnumerable<T> collection, int maxAttempts = 3, TimeSpan? interval = null)
        {
            var intervalToUse = interval ?? TimeSpan.FromSeconds(5);

            for (var i = 0; i < maxAttempts; ++i)
            {
                try
                {
                    Assert.Contains(collection, predicate);
                    return;
                }
                catch (ContainsException)
                {
                    if ((i + 1) == maxAttempts)
                        throw;
                }

                await Task.Delay(intervalToUse);
            }
        }
    }
}
