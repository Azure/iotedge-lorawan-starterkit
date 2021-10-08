// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanNetworkServer.Test.BasicStation
{
    using LoRaWan.NetworkServer.BasicStation.Controllers;
    using LoRaWan.NetworkServer.BasicStation.Processors;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using Moq;
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    public class BasicStationControllerTest
    {
        private Mock<BasicStationController> basicStationController;

        public BasicStationControllerTest()
        {
            var loggerMock = Mock.Of<ILogger<BasicStationController>>();
            var lnsProcessorMock = new Mock<ILnsProcessor>();
            basicStationController = new Mock<BasicStationController>(loggerMock, lnsProcessorMock.Object);
        }

        [Fact]
        public async Task CloseSocketAsync_WhenOpenSocket_ShouldClose()
        {
            // arrange
            var socketMock = new Mock<WebSocket>();
            socketMock.Setup(x => x.State).Returns(WebSocketState.Open);

            // act
            await basicStationController.Object.CloseSocketAsync(socketMock.Object, CancellationToken.None);

            // assert
            socketMock.Verify(x => x.CloseAsync(WebSocketCloseStatus.NormalClosure, nameof(WebSocketCloseStatus.NormalClosure), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CloseSocketAsync_WhenNonOpenSocket_ShouldNotClose()
        {
            // arrange
            var socketMock = new Mock<WebSocket>();
            socketMock.Setup(x => x.State).Returns(WebSocketState.Closed);

            // act
            await basicStationController.Object.CloseSocketAsync(socketMock.Object, CancellationToken.None);

            // assert
            socketMock.Verify(x => x.CloseAsync(WebSocketCloseStatus.NormalClosure, nameof(WebSocketCloseStatus.NormalClosure), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessIncomingRequestAsync_ShouldNotProcess_NonWebsocketRequests()
        {
            // arrange
            var httpContextMock = new Mock<HttpContext>();
            
            // mocking a non-websocket request
            var webSocketsManager = new Mock<WebSocketManager>();
            webSocketsManager.Setup(x => x.IsWebSocketRequest).Returns(false);
            httpContextMock.Setup(m => m.WebSockets).Returns(webSocketsManager.Object);

            // providing a mocked HttpResponse so that it's possible to verify stubbed properties
            var httpResponseMock = new Mock<HttpResponse>();
            httpResponseMock.SetupAllProperties();
            httpContextMock.Setup(m => m.Response).Returns(httpResponseMock.Object);

            basicStationController.Setup(c => c.GetHttpContext()).Returns(httpContextMock.Object);

            // act
            await basicStationController.Object.ProcessIncomingRequestAsync((string a, WebSocket s, CancellationToken t) => { return Task.CompletedTask; }, CancellationToken.None);

            // assert
            Assert.Equal(400, httpContextMock.Object.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessIncomingRequestAsync_ShouldProcess_WebsocketRequests()
        {
            // arrange
            var testString = "test";
            var testbytes = Encoding.UTF8.GetBytes(testString);
            var httpContextMock = new Mock<HttpContext>();

            // mocking a websocket request
            var webSocketsManager = new Mock<WebSocketManager>();
            var webSocketMock = new Mock<WebSocket>();
            // setting up the mock so that WebSocketRequests are "acceptable"
            webSocketsManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            webSocketsManager.Setup(x => x.AcceptWebSocketAsync()).ReturnsAsync(webSocketMock.Object);
            // initially the WebSocketState is Open
            webSocketMock.Setup(x => x.State).Returns(WebSocketState.Open);
            // when the CloseAsync is invoked, the State should be set to Closed (useful for verifying later on)
            webSocketMock.Setup(x => x.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                         .Callback<WebSocketCloseStatus, string, CancellationToken>((wscs, reason, c) =>
                         {
                             webSocketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
                             webSocketMock.Setup(x => x.CloseStatus).Returns(wscs);
                             webSocketMock.Setup(x => x.CloseStatusDescription).Returns(reason);
                         });
            // setting up the mock so that when ReceiveAsync is invoked the "testbytes" are written to the Memory portion
            webSocketMock.Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                         .Callback<Memory<byte>, CancellationToken>((m, c) => {
                             testbytes.CopyTo(m);
                         })
                         .ReturnsAsync(new ValueWebSocketReceiveResult(testbytes.Length, WebSocketMessageType.Text, true));
            httpContextMock.Setup(m => m.WebSockets).Returns(webSocketsManager.Object);
            
            // this is needed for logging the Basic Station (caller) remote ip address
            var connectionInfo = new Mock<ConnectionInfo>();
            connectionInfo.Setup(c => c.RemoteIpAddress).Returns(System.Net.IPAddress.Loopback);
            httpContextMock.Setup(m => m.Connection).Returns(connectionInfo.Object);

            basicStationController.Setup(c => c.GetHttpContext()).Returns(httpContextMock.Object);

            // act and assert
            await basicStationController.Object.ProcessIncomingRequestAsync((string input, WebSocket _, CancellationToken _) =>
                                                                                {
                                                                                    Assert.Equal(input, testString);
                                                                                    return Task.CompletedTask;
                                                                                },
                                                                            CancellationToken.None);

            // assert that websocket is closed, as the input string was verified through local function handler
            Assert.Equal(WebSocketState.Closed, webSocketMock.Object.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, webSocketMock.Object.CloseStatus);
        }

        [Fact]
        public async Task ProcessIncomingRequestAsync_ShouldProcess_LongWebsocketRequests()
        {
            // arrange
            var testString1 = "long";
            var testString2 = "test";
            var testbytes1 = Encoding.UTF8.GetBytes(testString1);
            var testbytes2 = Encoding.UTF8.GetBytes(testString2);
            var httpContextMock = new Mock<HttpContext>();

            // mocking a websocket request
            var webSocketsManager = new Mock<WebSocketManager>();
            var webSocketMock = new Mock<WebSocket>();
            // setting up the mock so that WebSocketRequests are "acceptable"
            webSocketsManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            webSocketsManager.Setup(x => x.AcceptWebSocketAsync()).ReturnsAsync(webSocketMock.Object);
            // initially the WebSocketState is Open
            webSocketMock.Setup(x => x.State).Returns(WebSocketState.Open);
            // when the CloseAsync is invoked, the State should be set to Closed (useful for verifying later on)
            webSocketMock.Setup(x => x.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                         .Callback<WebSocketCloseStatus, string, CancellationToken>((wscs, reason, c) =>
                         {
                             webSocketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
                             webSocketMock.Setup(x => x.CloseStatus).Returns(wscs);
                             webSocketMock.Setup(x => x.CloseStatusDescription).Returns(reason);
                         });
            // stting the mock so that when ReceiveAsync is called:
            // - first time it is a returning a partial WebSocketReceiveResult ("long")
            // - second time it is returning the final WebSocketReceiveResult ("test" and 'endOfMessage' set to true)
            var iterationsCount = 0;
            webSocketMock.Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                         .Callback<Memory<byte>, CancellationToken>((m, c) =>
                         {
                             if (iterationsCount == 0)
                             {
                                 testbytes1.CopyTo(m);
                             }
                             else
                             {
                                 testbytes2.CopyTo(m.Slice(testbytes1.Length));
                             };
                             iterationsCount++;
                         })
                         .ReturnsAsync(() => iterationsCount == 1 ? new ValueWebSocketReceiveResult(4, WebSocketMessageType.Text, false)
                                                                  : new ValueWebSocketReceiveResult(4, WebSocketMessageType.Text, true));

            httpContextMock.Setup(m => m.WebSockets).Returns(webSocketsManager.Object);
            
            // this is needed for logging the Basic Station (caller) remote ip address
            var connectionInfo = new Mock<ConnectionInfo>();
            connectionInfo.Setup(c => c.RemoteIpAddress).Returns(System.Net.IPAddress.Loopback);
            httpContextMock.Setup(m => m.Connection).Returns(connectionInfo.Object);

            basicStationController.Setup(c => c.GetHttpContext()).Returns(httpContextMock.Object);

            // act
            await basicStationController.Object.ProcessIncomingRequestAsync((string input, WebSocket _, CancellationToken _) =>
                                                                                {
                                                                                    Assert.Equal(string.Concat(testString1, testString2), input);
                                                                                    return Task.CompletedTask;
                                                                                },
                                                                            CancellationToken.None);

            // assert that websocket is closed, as the input string was already verified through local function handler
            Assert.Equal(WebSocketState.Closed, webSocketMock.Object.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, webSocketMock.Object.CloseStatus);
        }
    }
}
