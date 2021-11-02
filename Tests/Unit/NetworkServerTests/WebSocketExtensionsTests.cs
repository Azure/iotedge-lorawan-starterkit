// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Moq;
    using Xunit;

    public class WebSocketExtensionsTests
    {
        private readonly Mock<WebSocket> webSocketMock = new();

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(10)]
        public async Task ReadTextMessages_Stops_Enumerating_When_Socket_Closes(int numberOfChunks)
        {
            // arrange
            var message = JsonUtil.Strictify("{'foo':'bar'}");
            SetupWebSocketResponse(numberOfChunks, message);

            // act + assert
            await using var result = this.webSocketMock.Object.ReadTextMessages(CancellationToken.None);
            Assert.True(await result.MoveNextAsync());
            Assert.False(await result.MoveNextAsync());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(10)]
        public async Task ReadTextMessages_Accumulates_Messages_Independent_Of_Number_Of_Chunks(int numberOfChunks)
        {
            // arrange
            var message = JsonUtil.Strictify("{'foo':'bar'}");
            SetupWebSocketResponse(numberOfChunks, message);

            // act + assert
            await using var result = this.webSocketMock.Object.ReadTextMessages(CancellationToken.None);
            Assert.True(await result.MoveNextAsync());
            Assert.Equal(message, result.Current);
        }

        [Fact]
        public async Task ReadTextMessages_Throws_For_Unhandled_Message_Type()
        {
            // arrange
            _ = this.webSocketMock.Setup(ws => ws.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                                  .Returns(() => new ValueTask<ValueWebSocketReceiveResult>(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Binary, true)));

            // act
            await using var result = this.webSocketMock.Object.ReadTextMessages(CancellationToken.None);

            // assert
            await Assert.ThrowsAsync<NotSupportedException>(async () => await result.MoveNextAsync());
        }

        [Theory]
        [InlineData(3, 3)]
        [InlineData(1, 3)]
        [InlineData(3, 1)]
        public async Task ReadTextMessages_Iterates_Multiple_Messages(int numberOfMessages, int numberOfChunks)
        {
            // arrange
            var message = JsonUtil.Strictify("{'foo':'bar'}");
            SetupWebSocketResponse(numberOfChunks, Enumerable.Range(0, numberOfMessages).Select(_ => message).ToArray());

            // act + assert
            await using var result = this.webSocketMock.Object.ReadTextMessages(CancellationToken.None);
            for (var i = 0; i < numberOfMessages; ++i)
            {
                Assert.True(await result.MoveNextAsync());
                Assert.Equal(message, result.Current);
            }

            Assert.Equal(message, result.Current);
        }

        private void SetupWebSocketResponse(int numberOfChunks, params string[] messages)
        {
            var chunks =
                from m in messages
                select Encoding.UTF8.GetBytes(m).AsMemory()
                into bytes
                let chunkSize = (int)Math.Ceiling((double)bytes.Length / numberOfChunks)
                from chunk in Chunks(bytes, chunkSize).Append(Array.Empty<byte>())
                select (new ValueWebSocketReceiveResult(chunk.Length, WebSocketMessageType.Text, chunk.Length == 0), chunk);

            chunks = chunks.Append((new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true), Array.Empty<byte>()));

            var mockSequence = new MockSequence();

            foreach (var (result, source) in chunks)
            {
                _ = this.webSocketMock
                        .InSequence(mockSequence)
                        .Setup(ws => ws.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                        .Callback((Memory<byte> destination, CancellationToken _) => source.CopyTo(destination))
                        .Returns(() => new ValueTask<ValueWebSocketReceiveResult>(result));
            }

            static IEnumerable<ReadOnlyMemory<byte>> Chunks(ReadOnlyMemory<byte> source, int chunkSize)
            {
                while (!source.IsEmpty)
                {
                    var size = Math.Min(chunkSize, source.Length);
                    yield return source[..size];
                    source = source[size..];
                }
            }
        }
    }
}
