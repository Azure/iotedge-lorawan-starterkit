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
        private readonly Mock<WebSocket> webSocketMock;

        public WebSocketExtensionsTests()
        {
            this.webSocketMock = new Mock<WebSocket>();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        [InlineData(10)]
        public async Task ReadTextMessages_When_Socket_Closed_AsyncEnumerator_Should_Terminate(int numberOfChunks)
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
        public async Task ReadTextMessages_Should_Accumulate_Message_Independent_Of_Number_Of_Chunks(int numberOfChunks)
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
        public async Task ReadTextMessages_Should_Throw_For_Unhandled_WebSocketMessageType()
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
        public async Task ReadTextMessages_Should_Handle_Multiple_End_Of_Messages(int numberOfMessages, int numberOfChunks)
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
            var chunks = new List<(ValueWebSocketReceiveResult Result, byte[] Bytes)>();

            foreach (var m in messages)
            {
                var bytes = Encoding.UTF8.GetBytes(m);

                if (numberOfChunks < 1 || bytes.Length < numberOfChunks)
                    throw new ArgumentOutOfRangeException(nameof(numberOfChunks));

                var chunkSize = (int)Math.Ceiling((double)bytes.Length / numberOfChunks);
                // readjust effective number of chunks, taking ceiling of chunkSize into account.
                var adjustedNumberOfChunks = (bytes.Length / chunkSize) + 1;
                chunks.AddRange(Enumerable.Range(0, adjustedNumberOfChunks - 1)
                                          .Select(_ => new ValueWebSocketReceiveResult(chunkSize, WebSocketMessageType.Text, false))
                                          .Select((r, i) => (Result: r, Start: i * chunkSize, End: (i * chunkSize) + chunkSize))
                                          .Select(r => (r.Result, Bytes: bytes[r.Start..r.End])));

                // Last chunk can have a bytes count minus 1, depending on the chunk size and number of chunks.
                chunks.Add((Result: new ValueWebSocketReceiveResult(bytes.Length % chunkSize, WebSocketMessageType.Text, true),
                            Bytes: bytes[((adjustedNumberOfChunks - 1) * chunkSize)..]));
            }

            chunks.Add((Result: new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true), Bytes: Array.Empty<byte>()));

            var setup = new Queue<(ValueWebSocketReceiveResult Result, byte[] Bytes)>(chunks);

            var currentChunkIndex = 0;
            _ = this.webSocketMock.Setup(ws => ws.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                                  .Callback((Memory<byte> mem, CancellationToken _) =>
                                  {
                                      // Sending the WebSocketMessageType.Close message.
                                      if (currentChunkIndex == chunks.Count - 1) return;

                                      var m = new Memory<byte>(chunks[currentChunkIndex].Bytes);
                                      m.CopyTo(mem);
                                      ++currentChunkIndex;
                                  })
                                  .Returns(() => new ValueTask<ValueWebSocketReceiveResult>(setup.Dequeue().Result));
        }
    }
}
