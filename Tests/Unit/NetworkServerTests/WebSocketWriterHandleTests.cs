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

        private readonly WebSocketWriterRegistry<string, string> registry;
        private readonly WebSocketWriterHandle<string, string> sut;

        public WebSocketWriterHandleTests()
        {
            this.registry = new WebSocketWriterRegistry<string, string>(null);
            this.sut = new WebSocketWriterHandle<string, string>(this.registry, Key);
        }

        [Fact]
        public async Task SendAsync_Delegates_To_Registry()
        {
            // arrange
            const string message = "bar";
            using var cts = new CancellationTokenSource();
            var writer = new Mock<IWebSocketWriter<string>>();
            _ = this.registry.Register(Key, writer.Object);

            // act
            await this.sut.SendAsync(message, cts.Token);

            // assert
            writer.Verify(r => r.SendAsync(message, cts.Token), Times.Once);
        }

        [Fact]
        public void Equality_Comparison()
        {
            Assert.True(this.sut.Equals(new WebSocketWriterHandle<string, string>(this.registry, Key)));
            Assert.False(this.sut.Equals(new WebSocketWriterHandle<string, string>(this.registry, "anotherkey")));
            Assert.False(this.sut.Equals(new WebSocketWriterHandle<string, string>(new WebSocketWriterRegistry<string, string>(null), Key)));
            Assert.False(this.sut.Equals((object)null));
            Assert.False(this.sut.Equals(new object()));
        }
    }
}
