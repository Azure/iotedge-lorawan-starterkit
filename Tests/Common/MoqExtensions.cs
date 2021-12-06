// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Linq.Expressions;
    using System.Threading.Tasks;
    using Moq;

    public static class MoqExtensions
    {
        public static async Task RetryVerifyAsync<T>(this Mock<T> mock,
                                                     Expression<Action<T>> expression,
                                                     Func<Times> times,
                                                     int numberOfRetries = 5,
                                                     TimeSpan? delay = null)
            where T : class
        {
            if (mock is null) throw new ArgumentNullException(nameof(mock));

            var retryDelay = delay ?? TimeSpan.FromMilliseconds(50);
            for (var i = 0; i < numberOfRetries + 1; ++i)
            {
                try
                {
                    mock.Verify(expression, times);
                    break;
                }
                catch (MockException) when (i < numberOfRetries)
                {
                    // assertion does not yet pass, retry once more.
                    await Task.Delay(retryDelay);
                    continue;
                }
            }
        }
    }
}
