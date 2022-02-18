// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools;
    using Microsoft.AspNetCore.Http;
    using Moq;
    using Xunit;

    public sealed class WebSocketConnectionTests
    {
        private static readonly Func<HttpContext, WebSocket, CancellationToken, Task> NoopHandler = (_, _, _) => Task.CompletedTask;
        private readonly WebSocketConnection subject;
        private readonly Mock<HttpContext> httpContextMock;

        public WebSocketConnectionTests()
        {
            this.httpContextMock = new Mock<HttpContext>();
            this.subject = new WebSocketConnection(this.httpContextMock.Object, null);
        }

        [Fact]
        public async Task HandleAsync_Should_Return_400_If_Request_Is_Not_WebSocket()
        {
            // arrange
            SetupWebSocketConnection(true);

            // act
            var result = await this.subject.HandleAsync(NoopHandler, CancellationToken.None);

            // assert
            Assert.Equal((int)HttpStatusCode.BadRequest, this.httpContextMock.Object.Response.StatusCode);
            Assert.Equal((int)HttpStatusCode.BadRequest, result.Response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_Invokes_Handler()
        {
            // arrange
            using var cts = new CancellationTokenSource();
            var handler = new Mock<Func<HttpContext, WebSocket, CancellationToken, Task>>();
            var socket = SetupWebSocketConnection();

            // act
            _ = await this.subject.HandleAsync(handler.Object, cts.Token);

            // assert
            handler.Verify(h => h.Invoke(this.httpContextMock.Object, socket.Object, cts.Token), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_Handles_Premature_Connection_Close()
        {
            // arrange
            using var cts = new CancellationTokenSource();
            _ = SetupWebSocketConnection();

            // act + assert (does not throw)
            _ = await this.subject.HandleAsync((_, _, _) => throw new OperationCanceledException("Some exception", new WebSocketException(WebSocketError.ConnectionClosedPrematurely)), cts.Token);
        }

        [Fact]
        public async Task HandleAsync_Closes_Socket_After_Handling()
        {
            // arrange
            using var cts = new CancellationTokenSource();
            var socket = SetupWebSocketConnection();

            // act
            _ = await this.subject.HandleAsync(NoopHandler, cts.Token);

            // assert
            socket.Verify(s => s.CloseAsync(WebSocketCloseStatus.NormalClosure, It.IsAny<string>(), cts.Token), Times.Once);
        }


        private Mock<WebSocket> SetupWebSocketConnection(bool notWebSocketRequest = false)
        {
            var isWebSocketRequest = !notWebSocketRequest;
            var webSocketsManager = new Mock<WebSocketManager>();
            _ = webSocketsManager.SetupGet(x => x.IsWebSocketRequest).Returns(isWebSocketRequest);

            _ = this.httpContextMock.SetupGet(m => m.WebSockets).Returns(webSocketsManager.Object);
            _ = this.httpContextMock.SetupGet(m => m.Response).Returns(Mock.Of<HttpResponse>());

            var socketMock = new Mock<WebSocket>();
            if (isWebSocketRequest)
            {
                _ = webSocketsManager.Setup(x => x.AcceptWebSocketAsync()).ReturnsAsync(socketMock.Object);
                _ = this.httpContextMock.Setup(x => x.Connection).Returns(Mock.Of<ConnectionInfo>());
            }

            return socketMock;
        }
    }
}
