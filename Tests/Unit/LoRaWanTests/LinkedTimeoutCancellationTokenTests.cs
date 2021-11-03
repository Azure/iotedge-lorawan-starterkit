// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoRaWanTests
{
    using System;
    using System.Threading;
    using Xunit;

    public class LinkedTimeoutCancellationTokenTests
    {
        [Fact]
        public void Equality()
        {
            // arrange
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(42));
            var ts1 = TimeSpan.FromMilliseconds(42);
            var ts2 = TimeSpan.FromMilliseconds(43);

            // act + assert
            Assert.Equal(new TimeoutLinkedCancellationToken(ts1, cts.Token), new TimeoutLinkedCancellationToken(ts1, cts.Token));
            Assert.True(new TimeoutLinkedCancellationToken(ts1, cts.Token) == new TimeoutLinkedCancellationToken(ts1, cts.Token));
            Assert.False(new TimeoutLinkedCancellationToken(ts1, cts.Token) != new TimeoutLinkedCancellationToken(ts1, cts.Token));
            Assert.NotEqual(new TimeoutLinkedCancellationToken(ts1, cts.Token), new TimeoutLinkedCancellationToken(ts2, cts.Token));
            Assert.False(new TimeoutLinkedCancellationToken(ts1, cts.Token) == new TimeoutLinkedCancellationToken(ts2, cts.Token));
            Assert.True(new TimeoutLinkedCancellationToken(ts1, cts.Token) != new TimeoutLinkedCancellationToken(ts2, cts.Token));
        }

        [Theory]
        [InlineData(null            , false, false)]
        [InlineData(0               , false, true )]
        [InlineData(Timeout.Infinite, false, false)]
        [InlineData(null            , true , false)]
        [InlineData(0               , true , true )]
        [InlineData(Timeout.Infinite, true , false)]
        public void Cancellation_By_Timeout_Success_Cases(int? timeoutInMilliseconds,
                                                          bool withOtherCancellationToken,
                                                          bool isCancellationRequested)
        {
            var timeout = timeoutInMilliseconds is { } someTimeoutInMilliseconds
                        ? TimeSpan.FromMilliseconds(someTimeoutInMilliseconds)
                        : (TimeSpan?)null;

            using var cts = withOtherCancellationToken ? new CancellationTokenSource() : null;
            using var sut = new TimeoutLinkedCancellationToken(timeout, cts?.Token ?? CancellationToken.None);

            Assert.Equal(isCancellationRequested, sut.Token.IsCancellationRequested);
        }

        [Theory]
        [InlineData(42)]
        [InlineData(null)]
        public void Cancellation_By_Token_Success_Cases(int? timeoutInSeconds)
        {
            var timeout = timeoutInSeconds is { } someTimeoutInSeconds
                        ? TimeSpan.FromSeconds(someTimeoutInSeconds)
                        : (TimeSpan?)null;

            using var cts = new CancellationTokenSource();
            using var sut = new TimeoutLinkedCancellationToken(timeout, cts.Token);

            // assert 1
            Assert.False(sut.Token.IsCancellationRequested);

            // act
            cts.Cancel();

            // assert 2
            Assert.True(sut.Token.IsCancellationRequested);
        }
    }
}
