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
            SetupWebSocketResponse(message, numberOfChunks);

            // act + assert
            await using var result = WebSocketExtensions.ReadTextMessages(this.webSocketMock.Object, CancellationToken.None);
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
            SetupWebSocketResponse(message, numberOfChunks);

            // act + assert
            await using var result = WebSocketExtensions.ReadTextMessages(this.webSocketMock.Object, CancellationToken.None);
            Assert.True(await result.MoveNextAsync());
            Assert.Equal(message, result.Current);
        }

        private void SetupWebSocketResponse(string message, int numberOfChunks)
        {
            var bytes = Encoding.UTF8.GetBytes(message);

            if (numberOfChunks < 1 || bytes.Length < numberOfChunks)
                throw new ArgumentOutOfRangeException(nameof(numberOfChunks));

            var chunkSize = (int)Math.Ceiling((double)bytes.Length / numberOfChunks);
            // readjust effective number of chunks, taking ceiling of chunkSize into account.
            numberOfChunks = (bytes.Length / chunkSize) + 1;
            var chunks =
                Enumerable.Range(0, numberOfChunks - 1)
                          .Select(_ => new ValueWebSocketReceiveResult(chunkSize, WebSocketMessageType.Text, false))
                          .ToList();

            chunks.Add(new ValueWebSocketReceiveResult(bytes.Length % chunkSize, WebSocketMessageType.Text, true));
            chunks.Add(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            var setup = new Queue<ValueWebSocketReceiveResult>(chunks);

            var currentChunkIndex = 0;
            _ = this.webSocketMock.Setup(ws => ws.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                                  .Callback((Memory<byte> mem, CancellationToken _) =>
                                  {
                                      // Sending the WebSocketMessageType.Close message.
                                      if (currentChunkIndex == numberOfChunks) return;

                                      var start = currentChunkIndex * chunkSize;
                                      var end = start + chunks[currentChunkIndex].Count;
                                      var m = new Memory<byte>(bytes[start..end]);
                                      m.CopyTo(mem);
                                      ++currentChunkIndex;
                                  })
                                  .Returns(() => new ValueTask<ValueWebSocketReceiveResult>(setup.Dequeue()));
        }
    }
}
