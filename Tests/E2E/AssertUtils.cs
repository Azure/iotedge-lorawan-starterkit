// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

public static class AssertUtils
{
    public static async Task ContainsWithRetriesAsync<T>(T expected, IEnumerable<T> collection, int maxAttempts = 3, TimeSpan? interval = null)
    {
        var intervalToUse = interval ?? TimeSpan.FromSeconds(5);

        for (var i = 0; i < maxAttempts; ++i)
        {
            try
            {
                Assert.Contains<T>(expected, collection);
                return;
            }
            catch (ContainsException)
            {
                if ((i + 1) == maxAttempts)
                    throw;
            }

            await Task.Delay(intervalToUse);
            intervalToUse += intervalToUse;
        }
    }

    public static async Task ContainsWithRetriesAsync<T>(Predicate<T> predicate, IEnumerable<T> collection, int maxAttempts = 3, TimeSpan? interval = null)
    {
        var intervalToUse = interval ?? TimeSpan.FromSeconds(5);

        for (var i = 0; i < maxAttempts; ++i)
        {
            try
            {
                Assert.Contains<T>(collection, predicate);
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