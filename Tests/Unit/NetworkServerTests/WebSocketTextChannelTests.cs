// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class WebSocketTextChannelTests
    {
        private readonly Mock<WebSocket> webSocketMock;
        private readonly WebSocketTextChannel sut;

        public WebSocketTextChannelTests()
        {
            this.webSocketMock = new Mock<WebSocket>();
            this.sut = new WebSocketTextChannel(this.webSocketMock.Object, Timeout.InfiniteTimeSpan);
        }

        [Fact]
        public void Constructor_Disallows_Invalid_Send_Timeouts()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WebSocketTextChannel(this.webSocketMock.Object, TimeSpan.FromSeconds(-10)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        public void Constructor_Allows_Valid_Send_Timeouts(int timeoutInSeconds)
        {
            Assert.NotNull(new WebSocketTextChannel(this.webSocketMock.Object, TimeSpan.FromSeconds(timeoutInSeconds)));
        }

        [Fact]
        public void Constructor_Allows_Infinite_Send_Timeout()
        {
            Assert.NotNull(new WebSocketTextChannel(this.webSocketMock.Object, Timeout.InfiniteTimeSpan));
        }

        [Theory]
        [InlineData(WebSocketState.Open, false)]
        [InlineData(WebSocketState.Closed, true)]
        public void IsClosed_Reflects_Underlying_Socket_State(WebSocketState webSocketState, bool isClosed)
        {
            // arrange
            _ = this.webSocketMock.SetupGet(ws => ws.State).Returns(webSocketState);

            // act
            var result = this.sut.IsClosed;

            // assert
            Assert.Equal(isClosed, result);
        }

        [Fact]
        public async Task ProcessSendQueueAsync_Stops_When_Cancelled()
        {
            // arrange
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

            // act
            Task Act() => this.sut.ProcessSendQueueAsync(cts.Token);

            // assert
            _ = await Assert.ThrowsAsync<OperationCanceledException>(Act);
        }

        [Fact]
        public async Task SendAsync_Fails_If_Send_Queue_Is_Not_Being_Processed()
        {
            // arrange + act
            async Task Act() => await this.sut.SendAsync("foo", CancellationToken.None);

            // assert
            _ = await Assert.ThrowsAsync<InvalidOperationException>(Act);
        }

        [Fact]
        public async Task SendAsync_Fails_If_Send_Queue_Is_Not_Being_Processed_Anymore()
        {
            // act + assert 1
            using (var t = UseProcessSendQueueListener())
            {
                // Sending should succeed while someone is listening on ProcessSendQueue.
                await this.sut.SendAsync("foo", CancellationToken.None);
            }

            // act + assert 2
            // Sending should fail when stopped listening on ProcessSendQueue.
            _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await this.sut.SendAsync("bar", CancellationToken.None));
        }

        [Fact]
        public async Task SendAsync_Handles_Concurrent_Operations_If_Send_Queue_Is_Being_Processed()
        {
            // arrange
            const int numberOfConcurrentSends = 5;
            using var t = UseProcessSendQueueListener();
            var messages = Enumerable.Range(0, numberOfConcurrentSends)
                                     .Select(i => i.ToString(CultureInfo.InvariantCulture))
                                     .ToList();

            // act
            await Task.WhenAll(from m in messages
                               select this.sut.SendAsync(m, CancellationToken.None).AsTask());

            // assert
            foreach (var m in messages)
            {
                this.webSocketMock.Verify(ws => ws.SendAsync(It.Is((ArraySegment<byte> bytes) => Encoding.UTF8.GetString(bytes) == m),
                                                             WebSocketMessageType.Text,
                                                             true, CancellationToken.None), Times.Once);
            }
        }

        [Fact]
        public async Task SendAsync_Is_Resilient_To_Single_Message_Failure()
        {
            // arrange
            const int numberOfConcurrentSends = 5;
            using var t = UseProcessSendQueueListener();
            _ = this.webSocketMock.Setup(ws => ws.SendAsync(It.IsAny<ArraySegment<byte>>(),
                                                            It.IsAny<WebSocketMessageType>(),
                                                            It.IsAny<bool>(),
                                                            It.IsAny<CancellationToken>()))
                                  .Throws(new WebSocketException());
            var messages = Enumerable.Range(0, numberOfConcurrentSends)
                                     .Select(i => i.ToString(CultureInfo.InvariantCulture))
                                     .ToList();

            // act
            var tasks = messages.Select(m => this.sut.SendAsync(m, CancellationToken.None).AsTask())
                                .ToArray();

            // assert
            foreach (var (m, task) in messages.Zip(tasks))
            {
                this.webSocketMock.Verify(ws => ws.SendAsync(It.Is((ArraySegment<byte> bytes) => Encoding.UTF8.GetString(bytes) == m),
                                                             WebSocketMessageType.Text,
                                                             true, CancellationToken.None), Times.Once);
                _ = await Assert.ThrowsAsync<WebSocketException>(() => task);
            }
        }

        [Fact]
        public async Task SendAsync_Timeout_Causes_Cancellation()
        {
            // arrange
            var sut = new WebSocketTextChannel(this.webSocketMock.Object, TimeSpan.Zero);
            _ = this.webSocketMock.Setup(ws => ws.SendAsync(It.IsAny<ArraySegment<byte>>(),
                                                            It.IsAny<WebSocketMessageType>(),
                                                            It.IsAny<bool>(),
                                                            It.Is((CancellationToken ct) => ct.IsCancellationRequested)))
                                  .Throws(new OperationCanceledException());
            using var t = UseProcessSendQueueListener(sut);

            // act
            async Task Act() => await sut.SendAsync("foo", CancellationToken.None);

            // assert
            _ = await Assert.ThrowsAsync<TaskCanceledException>(Act);
        }

        private IDisposable UseProcessSendQueueListener(WebSocketTextChannel webSocketTextChannel = null) =>
            new ProcessSendQueueListener(webSocketTextChannel ?? this.sut);

        private sealed class ProcessSendQueueListener : IDisposable
        {
            private readonly CancellationTokenSource cts;
            private readonly Task webSocketTextChannelListenTask;

            public ProcessSendQueueListener(WebSocketTextChannel webSocketTextChannel)
            {
                this.cts = new CancellationTokenSource();
                this.webSocketTextChannelListenTask = webSocketTextChannel.ProcessSendQueueAsync(cts.Token);
            }

            public void Dispose()
            {
                this.cts.Cancel();
                _ = Assert.ThrowsAsync<OperationCanceledException>(() => this.webSocketTextChannelListenTask).GetAwaiter().GetResult();
                this.cts.Dispose();
            }
        }
    }
}
