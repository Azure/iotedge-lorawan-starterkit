// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Xunit;

    public class LinkedTimeoutCancellationTokenTests
    {
        /*[Fact]
        public void Equality()
        {
            // arrange
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(42));
            var ts1 = TimeSpan.FromMilliseconds(42);
            var ts2 = TimeSpan.FromMilliseconds(43);

            // act + assert
            Assert.Equal(new LinkedTimeoutCancellationToken(ts1, cts.Token), new LinkedTimeoutCancellationToken(ts1, cts.Token));
            Assert.True(new LinkedTimeoutCancellationToken(ts1, cts.Token) == new LinkedTimeoutCancellationToken(ts1, cts.Token));
            Assert.False(new LinkedTimeoutCancellationToken(ts1, cts.Token) != new LinkedTimeoutCancellationToken(ts1, cts.Token));
            Assert.NotEqual(new LinkedTimeoutCancellationToken(ts1, cts.Token), new LinkedTimeoutCancellationToken(ts2, cts.Token));
            Assert.False(new LinkedTimeoutCancellationToken(ts1, cts.Token) == new LinkedTimeoutCancellationToken(ts2, cts.Token));
            Assert.True(new LinkedTimeoutCancellationToken(ts1, cts.Token) != new LinkedTimeoutCancellationToken(ts2, cts.Token));
        }*/

        public static IEnumerable<object[]> CancellationByTimeoutSuccessCases()
        {
            using var cts = new CancellationTokenSource();
            yield return new object[] { new LinkedTimeoutCancellationToken(null, CancellationToken.None), false };
            yield return new object[] { new LinkedTimeoutCancellationToken(TimeSpan.Zero, CancellationToken.None), true };
            yield return new object[] { new LinkedTimeoutCancellationToken(null, cts.Token), false };
            yield return new object[] { new LinkedTimeoutCancellationToken(TimeSpan.Zero, cts.Token), true };
        }

        [Theory]
        [MemberData(nameof(CancellationByTimeoutSuccessCases))]
        public void Cancellation_By_Timeout_Success_Cases(LinkedTimeoutCancellationToken sut, bool cancellationRequested)
        {
            try
            {
                Assert.Equal(cancellationRequested, sut.Token.IsCancellationRequested);
            }
            finally
            {
                sut.Dispose();
            }
        }

        public static IEnumerable<object[]> CancellationByTokenSuccessCases()
        {
            var cts1 = new CancellationTokenSource();
            yield return new object[] { new LinkedTimeoutCancellationToken(TimeSpan.FromSeconds(42), cts1.Token), cts1 };
            var cts2 = new CancellationTokenSource();
            yield return new object[] { new LinkedTimeoutCancellationToken(null, cts2.Token), cts2 };
        }

        [Theory]
        [MemberData(nameof(CancellationByTokenSuccessCases))]
        public void Cancellation_By_Token_Success_Cases(LinkedTimeoutCancellationToken sut, CancellationTokenSource cts)
        {
            try
            {
                // assert 1
                Assert.False(sut.Token.IsCancellationRequested);

                // act
                cts.Cancel();

                // assert 2
                Assert.True(sut.Token.IsCancellationRequested);
            }
            finally
            {
                sut.Dispose();
                cts.Dispose();
            }
        }
    }
}
