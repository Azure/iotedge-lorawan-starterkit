// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class WebSocketWriterHandleTests
    {
        [Fact]
        public async Task SendAsync_Delegates_To_Registry()
        {
            // arrange
            const string message = "bar";
            using var cts = new CancellationTokenSource();
            var registry = new WebSocketWriterRegistry<string, string>(null);
            var writerMock = new Mock<IWebSocketWriter<string>>();
            var sut = registry.Register("foo", writerMock.Object);

            // act
            await sut.SendAsync(message, cts.Token);

            // assert
            writerMock.Verify(r => r.SendAsync(message, cts.Token), Times.Once);
        }

        [Fact]
        public void Equality_Comparison()
        {
            var registry = new
            {
                A = new WebSocketWriterRegistry<string, string>(null),
                B = new WebSocketWriterRegistry<string, string>(null),
            };

            var sut = registry.A.Register("a", new Mock<IWebSocketWriter<string>>().Object);
            var handle1 = registry.A.Register("b", new Mock<IWebSocketWriter<string>>().Object);
            var handle2 = registry.B.Register("a", new Mock<IWebSocketWriter<string>>().Object);

            Assert.Equal(sut, sut);
            Assert.NotEqual(sut, handle1);
            Assert.NotEqual(sut, handle2);
            Assert.False(sut.Equals(null));
            Assert.False(sut.Equals(new object()));
        }
    }
}
