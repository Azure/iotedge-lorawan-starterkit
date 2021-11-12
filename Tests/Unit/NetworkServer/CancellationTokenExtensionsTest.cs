// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading;
    using LoRaWan.NetworkServer;
    using Xunit;

    public class CancellationTokenExtensionsTest
    {
        [Fact]
        public void LinkWithTimeout_Throws_For_Invalid_Timeout()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                _ = CancellationToken.None.LinkWithTimeout(TimeSpan.FromSeconds(-1)));

            Assert.Equal("timeout", ex.ParamName);
        }

        [Theory]
        [InlineData(null            , false, false)]
        [InlineData(0               , false, true )]
        [InlineData(Timeout.Infinite, false, false)]
        [InlineData(null            , true , false)]
        [InlineData(0               , true , true )]
        [InlineData(Timeout.Infinite, true , false)]
        public void LinkWithTimeout_Timeout_Cancellation(int? timeoutInMilliseconds,
                                                         bool withOtherCancellationToken,
                                                         bool isCancellationRequested)
        {
            var timeout = timeoutInMilliseconds is { } someTimeoutInMilliseconds
                        ? TimeSpan.FromMilliseconds(someTimeoutInMilliseconds)
                        : (TimeSpan?)null;

            using var cts = withOtherCancellationToken ? new CancellationTokenSource() : null;
            using var sut = (cts?.Token ?? CancellationToken.None).LinkWithTimeout(timeout);
            CancellationToken cancellationToken = sut;

            Assert.Equal(isCancellationRequested, cancellationToken.IsCancellationRequested);
        }

        [Theory]
        [InlineData(42)]
        [InlineData(null)]
        public void LinkWithTimeout_Token_Cancellation(int? timeoutInSeconds)
        {
            var timeout = timeoutInSeconds is { } someTimeoutInSeconds
                        ? TimeSpan.FromSeconds(someTimeoutInSeconds)
                        : (TimeSpan?)null;

            using var cts = new CancellationTokenSource();
            using var sut = cts.Token.LinkWithTimeout(timeout);
            CancellationToken cancellationToken = sut;

            // assert 1
            Assert.False(cancellationToken.IsCancellationRequested);

            // act
            cts.Cancel();

            // assert 2
            Assert.True(cancellationToken.IsCancellationRequested);
        }
    }
}
