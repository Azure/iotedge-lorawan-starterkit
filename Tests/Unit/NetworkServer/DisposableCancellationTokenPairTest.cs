// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class DisposableCancellationTokenPairTest
    {
        [Fact]
        public void Convertible_To_CancellationToken()
        {
            using var cts = new CancellationTokenSource();
            using var pair = new DisposableCancellationTokenPair(null, cts.Token);

            Assert.Equal(cts.Token, pair);
        }

        [Fact]
        public void Dispose_Disposes_Disposable()
        {
            var disposableMock = new Mock<IDisposable>();

            var pair = new DisposableCancellationTokenPair(disposableMock.Object, CancellationToken.None);
            pair.Dispose();

            disposableMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Fact]
        public void Dispose_Has_No_Effect_With_Null_Disposable()
        {
            var pair = new DisposableCancellationTokenPair(null, CancellationToken.None);

            var ex = Record.Exception(() => pair.Dispose());

            Assert.Null(ex);
        }

        [Fact]
        public void Equality()
        {
            using var cts1 = new CancellationTokenSource();
            using var pair1 = new DisposableCancellationTokenPair(cts1, cts1.Token);

            using var cts2 = new CancellationTokenSource();
            using var pair2 = new DisposableCancellationTokenPair(cts2, cts2.Token);

            Assert.True(pair1.Equals(pair1));
            Assert.True(pair1.Equals((object)pair1));

            Assert.False(pair1.Equals(pair2));
            Assert.False(pair1.Equals((object)pair2));
            Assert.False(pair1.Equals(new object()));
            Assert.False(pair1.Equals(null));

            Assert.False(pair1 == pair2);
            Assert.True(pair1 != pair2);
        }
    }
}
