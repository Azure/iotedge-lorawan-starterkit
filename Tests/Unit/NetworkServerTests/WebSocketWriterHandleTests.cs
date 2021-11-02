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
        private const string Key = "foo";

        private readonly Mock<IWebSocketWriterRegistry<string, string>> registryMock;
        private readonly WebSocketWriterHandle<string, string> sut;

        public WebSocketWriterHandleTests()
        {
            this.registryMock = new Mock<IWebSocketWriterRegistry<string, string>>();
            this.sut = new WebSocketWriterHandle<string, string>(this.registryMock.Object, Key);
        }

        [Fact]
        public async Task SendAsync_Delegates_To_Registry()
        {
            // arrange
            const string message = "bar";
            using var cts = new CancellationTokenSource();

            // act
            await this.sut.SendAsync(message, cts.Token);

            // assert
            this.registryMock.Verify(r => r.SendAsync(Key, message, cts.Token), Times.Once);
        }

        [Fact]
        public void Equality_Comparison()
        {
            Assert.True(this.sut.Equals(new WebSocketWriterHandle<string, string>(this.registryMock.Object, Key)));
            Assert.False(this.sut.Equals(new WebSocketWriterHandle<string, string>(this.registryMock.Object, "anotherkey")));
            Assert.False(this.sut.Equals(new WebSocketWriterHandle<string, string>(new Mock<IWebSocketWriterRegistry<string, string>>().Object, Key)));
            Assert.False(this.sut.Equals((object)null));
            Assert.False(this.sut.Equals(this.registryMock));
        }
    }
}
