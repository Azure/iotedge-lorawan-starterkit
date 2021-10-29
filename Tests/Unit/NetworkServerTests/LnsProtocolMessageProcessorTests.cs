// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests
{
    using System;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class LnsProtocolMessageProcessorTests
    {
        private readonly Mock<IBasicsStationConfigurationService> basicsStationConfigurationMock;
        private readonly LnsProtocolMessageProcessor lnsMessageProcessorMock;
        private readonly Mock<WebSocket> socketMock;
        private readonly Mock<HttpContext> httpContextMock;

        public LnsProtocolMessageProcessorTests()
        {
            var loggerMock = Mock.Of<ILogger<LnsProtocolMessageProcessor>>();
            this.socketMock = new Mock<WebSocket>();
            this.httpContextMock = new Mock<HttpContext>();
            this.basicsStationConfigurationMock = new Mock<IBasicsStationConfigurationService>();

            this.lnsMessageProcessorMock = new LnsProtocolMessageProcessor(basicsStationConfigurationMock.Object,
                                                                           loggerMock);
        }

        [Fact]
        public async Task CloseSocketAsync_WhenOpenSocket_ShouldClose()
        {
            // arrange
            this.socketMock.Setup(x => x.State).Returns(WebSocketState.Open);

            // act
            await this.lnsMessageProcessorMock.CloseSocketAsync(this.socketMock.Object, CancellationToken.None);

            // assert
            this.socketMock.Verify(x => x.CloseAsync(WebSocketCloseStatus.NormalClosure, nameof(WebSocketCloseStatus.NormalClosure), It.IsAny<CancellationToken>()));
        }

        [Fact]
        public async Task CloseSocketAsync_WhenNonOpenSocket_ShouldNotClose()
        {
            // arrange
            this.socketMock.Setup(x => x.State).Returns(WebSocketState.Closed);

            // act
            await this.lnsMessageProcessorMock.CloseSocketAsync(this.socketMock.Object, CancellationToken.None);

            // assert
            this.socketMock.Verify(x => x.CloseAsync(WebSocketCloseStatus.NormalClosure, nameof(WebSocketCloseStatus.NormalClosure), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ProcessIncomingRequestAsync_ShouldNotProcess_NonWebsocketRequests()
        {
            // mocking a non-websocket request
            var webSocketsManager = new Mock<WebSocketManager>();
            webSocketsManager.Setup(x => x.IsWebSocketRequest).Returns(false);
            this.httpContextMock.Setup(m => m.WebSockets).Returns(webSocketsManager.Object);

            // providing a mocked HttpResponse so that it's possible to verify stubbed properties
            var httpResponseMock = new Mock<HttpResponse>();
            httpResponseMock.SetupAllProperties();
            this.httpContextMock.Setup(m => m.Response).Returns(httpResponseMock.Object);

            // act
            await this.lnsMessageProcessorMock.ProcessIncomingRequestAsync(this.httpContextMock.Object,
                                                                           (HttpContext _, string a, WebSocket s, CancellationToken t) => { return Task.FromResult(false); },
                                                                           CancellationToken.None);

            // assert
            Assert.Equal(400, this.httpContextMock.Object.Response.StatusCode);
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
            // setting up the mock so that WebSocketRequests are "acceptable"
            webSocketsManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            webSocketsManager.Setup(x => x.AcceptWebSocketAsync()).ReturnsAsync(this.socketMock.Object);
            // initially the WebSocketState is Open
            this.socketMock.Setup(x => x.State).Returns(WebSocketState.Open);
            // when the CloseAsync is invoked, the State should be set to Closed (useful for verifying later on)
            this.socketMock.Setup(x => x.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                         .Callback<WebSocketCloseStatus, string, CancellationToken>((wscs, reason, c) =>
                         {
                             this.socketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
                             this.socketMock.Setup(x => x.CloseStatus).Returns(wscs);
                             this.socketMock.Setup(x => x.CloseStatusDescription).Returns(reason);
                         });
            // setting up the mock so that when ReceiveAsync is invoked the "testbytes" are written to the Memory portion
            this.socketMock.Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                         .Callback<Memory<byte>, CancellationToken>((m, c) =>
                         {
                             testbytes.CopyTo(m);
                         })
                         .ReturnsAsync(new ValueWebSocketReceiveResult(testbytes.Length, WebSocketMessageType.Text, true));
            httpContextMock.Setup(m => m.WebSockets).Returns(webSocketsManager.Object);

            // this is needed for logging the Basic Station (caller) remote ip address
            var connectionInfo = new Mock<ConnectionInfo>();
            connectionInfo.Setup(c => c.RemoteIpAddress).Returns(System.Net.IPAddress.Loopback);
            httpContextMock.Setup(m => m.Connection).Returns(connectionInfo.Object);

            // act and assert
            await this.lnsMessageProcessorMock.ProcessIncomingRequestAsync(httpContextMock.Object,
                                                                           (HttpContext _, string input, WebSocket _, CancellationToken _) =>
                                                                           {
                                                                               Assert.Equal(input, testString);
                                                                               return Task.FromResult(false);
                                                                           },
                                                                           CancellationToken.None);

            // assert that websocket is closed, as the input string was verified through local function handler
            Assert.Equal(WebSocketState.Closed, this.socketMock.Object.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, this.socketMock.Object.CloseStatus);
        }



        [Fact]
        public async Task ProcessIncomingRequestAsync_ShouldNotExecuteHandler_WhenConnectionClosedPrematurelyException()
        {
            // arrange
            var testString = "test";
            var testbytes = Encoding.UTF8.GetBytes(testString);
            var httpContextMock = new Mock<HttpContext>();

            // mocking a websocket request
            var webSocketsManager = new Mock<WebSocketManager>();
            // setting up the mock so that WebSocketRequests are "acceptable"
            webSocketsManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            webSocketsManager.Setup(x => x.AcceptWebSocketAsync()).ReturnsAsync(this.socketMock.Object);
            // initially the WebSocketState is Open
            this.socketMock.Setup(x => x.State).Returns(WebSocketState.Open);
            // setting up the mock so that when ReceiveAsync is invoked the "testbytes" are written to the Memory portion
            this.socketMock.Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                         .Callback<Memory<byte>, CancellationToken>((m, c) =>
                         {
                             var innerException = new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
                             throw new OperationCanceledException("Mocked exception", innerException);
                         })
                         .ReturnsAsync(new ValueWebSocketReceiveResult(testbytes.Length, WebSocketMessageType.Text, true));
            httpContextMock.Setup(m => m.WebSockets).Returns(webSocketsManager.Object);

            // this is needed for logging the Basic Station (caller) remote ip address
            var connectionInfo = new Mock<ConnectionInfo>();
            connectionInfo.Setup(c => c.RemoteIpAddress).Returns(System.Net.IPAddress.Loopback);
            httpContextMock.Setup(m => m.Connection).Returns(connectionInfo.Object);

            // act and assert
            await this.lnsMessageProcessorMock.ProcessIncomingRequestAsync(httpContextMock.Object,
                                                                           (HttpContext _, string _, WebSocket _, CancellationToken _) =>
                                                                           {
                                                                               // this assertion will fail only if we reach the handler
                                                                               // which should not be the case for prematurely ended connections
                                                                               Assert.True(false);
                                                                               return Task.FromResult(false);
                                                                           },
                                                                           CancellationToken.None);
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
            // setting up the mock so that WebSocketRequests are "acceptable"
            webSocketsManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            webSocketsManager.Setup(x => x.AcceptWebSocketAsync()).ReturnsAsync(this.socketMock.Object);
            // initially the WebSocketState is Open
            this.socketMock.Setup(x => x.State).Returns(WebSocketState.Open);
            // when the CloseAsync is invoked, the State should be set to Closed (useful for verifying later on)
            this.socketMock.Setup(x => x.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                         .Callback<WebSocketCloseStatus, string, CancellationToken>((wscs, reason, c) =>
                         {
                             this.socketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
                             this.socketMock.Setup(x => x.CloseStatus).Returns(wscs);
                             this.socketMock.Setup(x => x.CloseStatusDescription).Returns(reason);
                         });
            // stting the mock so that when ReceiveAsync is called:
            // - first time it is a returning a partial WebSocketReceiveResult ("long")
            // - second time it is returning the final WebSocketReceiveResult ("test" and 'endOfMessage' set to true)
            var iterationsCount = 0;
            this.socketMock.Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                         .Callback<Memory<byte>, CancellationToken>((m, c) =>
                         {
                             if (iterationsCount == 0)
                             {
                                 testbytes1.CopyTo(m);
                             }
                             else
                             {
                                 testbytes2.CopyTo(m);
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

            // act
            await this.lnsMessageProcessorMock.ProcessIncomingRequestAsync(httpContextMock.Object,
                                                                           (HttpContext _, string input, WebSocket _, CancellationToken _) =>
                                                                               {
                                                                                   Assert.Equal(string.Concat(testString1, testString2), input);
                                                                                   return Task.FromResult(false);
                                                                               },
                                                                           CancellationToken.None);

            // assert that websocket is closed, as the input string was already verified through local function handler
            Assert.Equal(WebSocketState.Closed, this.socketMock.Object.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, this.socketMock.Object.CloseStatus);
        }

        [Fact]
        public async Task ProcessIncomingRequestAsync_ShouldProcess_TwoShortWebsocketRequests_WithoutClosingSocketInBetween()
        {
            // arrange
            var testString1 = "shortMessage1";
            var testString2 = "shortMessage2";
            var testbytes1 = Encoding.UTF8.GetBytes(testString1);
            var testbytes2 = Encoding.UTF8.GetBytes(testString2);
            var httpContextMock = new Mock<HttpContext>();

            // mocking a websocket request
            var webSocketsManager = new Mock<WebSocketManager>();
            // setting up the mock so that WebSocketRequests are "acceptable"
            webSocketsManager.Setup(x => x.IsWebSocketRequest).Returns(true);
            webSocketsManager.Setup(x => x.AcceptWebSocketAsync()).ReturnsAsync(this.socketMock.Object);
            // initially the WebSocketState is Open
            this.socketMock.Setup(x => x.State).Returns(WebSocketState.Open);
            // when the CloseAsync is invoked, the State should be set to Closed (useful for verifying later on)
            this.socketMock.Setup(x => x.CloseAsync(It.IsAny<WebSocketCloseStatus>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                         .Callback<WebSocketCloseStatus, string, CancellationToken>((wscs, reason, c) =>
                         {
                             this.socketMock.Setup(x => x.State).Returns(WebSocketState.Closed);
                             this.socketMock.Setup(x => x.CloseStatus).Returns(wscs);
                             this.socketMock.Setup(x => x.CloseStatusDescription).Returns(reason);
                         });
            // stting the mock so that when ReceiveAsync is called:
            // - first time it is a returning a partial WebSocketReceiveResult ("long")
            // - second time it is returning the final WebSocketReceiveResult ("test" and 'endOfMessage' set to true)
            var iterationsCount = 0;
            this.socketMock.Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                         .Callback<Memory<byte>, CancellationToken>((m, c) =>
                         {
                             if (iterationsCount == 0)
                             {
                                 testbytes1.CopyTo(m);
                             }
                             else
                             {
                                 testbytes2.CopyTo(m);
                             };
                             iterationsCount++;
                         })
                         .ReturnsAsync(() => iterationsCount == 1 ? new ValueWebSocketReceiveResult(testString1.Length, WebSocketMessageType.Text, true)
                                                                  : new ValueWebSocketReceiveResult(testString2.Length, WebSocketMessageType.Text, true));

            httpContextMock.Setup(m => m.WebSockets).Returns(webSocketsManager.Object);

            // this is needed for logging the Basic Station (caller) remote ip address
            var connectionInfo = new Mock<ConnectionInfo>();
            connectionInfo.Setup(c => c.RemoteIpAddress).Returns(System.Net.IPAddress.Loopback);
            httpContextMock.Setup(m => m.Connection).Returns(connectionInfo.Object);
            var handlerInvokationCount = 0;

            // act
            await this.lnsMessageProcessorMock.ProcessIncomingRequestAsync(httpContextMock.Object,
                                                                           (HttpContext _, string input, WebSocket _, CancellationToken _) =>
                                                                           {
                                                                               if (handlerInvokationCount++ == 0)
                                                                               {
                                                                                   Assert.Equal(testString1, input);
                                                                                   return Task.FromResult(true);
                                                                               }
                                                                               else
                                                                               {
                                                                                   Assert.Equal(testString2, input);
                                                                                   return Task.FromResult(false);
                                                                               }
                                                                           },
                                                                           CancellationToken.None);

            // assert that websocket is closed, as the input string was already verified through local function handler
            Assert.Equal(WebSocketState.Closed, this.socketMock.Object.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, this.socketMock.Object.CloseStatus);
            Assert.Equal(2, handlerInvokationCount);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task InternalHandleDiscoveryAsync_ShouldSendProperJson(bool isHttps, bool isValidNic)
        {
            // arrange
            var inputJsonString = @"{ ""router"": ""b827:ebff:fee1:e39a"" }";
            InitializeConfigurationServiceMock();

            // mocking localIpAddress
            var connectionInfoMock = new Mock<ConnectionInfo>();
            var firstNic = isValidNic ? NetworkInterface.GetAllNetworkInterfaces()
                                                        .FirstOrDefault()
                                      : null;
            if (firstNic is not null)
            {
                var firstNicIp = firstNic.GetIPProperties().UnicastAddresses.First().Address;
                connectionInfoMock.SetupGet(ci => ci.LocalIpAddress).Returns(firstNicIp);
                this.httpContextMock.Setup(h => h.Connection).Returns(connectionInfoMock.Object);
            }
            else
            {
                connectionInfoMock.SetupGet(ci => ci.LocalIpAddress).Returns(new System.Net.IPAddress(new byte[] { 192, 168, 1, 10 }));
                this.httpContextMock.Setup(h => h.Connection).Returns(connectionInfoMock.Object);
            }

            var mockHttpRequest = new Mock<HttpRequest>();
            mockHttpRequest.SetupGet(x => x.Host).Returns(new HostString("localhost", 1234));
            mockHttpRequest.SetupGet(x => x.IsHttps).Returns(isHttps);
            this.httpContextMock.Setup(h => h.Request).Returns(mockHttpRequest.Object);


            // intercepting the SendAsync to verify that what we sent is actually what we expected
            var sentString = string.Empty;
            this.socketMock.Setup(x => x.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                           .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((message, type, end, _) =>
                           {
                               sentString = Encoding.UTF8.GetString(message);
                               Assert.Equal(WebSocketMessageType.Text, type);
                               Assert.True(end);
                           });

            var muxs = Id6.Format(firstNic?.GetPhysicalAddress().Convert48To64() ?? 0, Id6.FormatOptions.FixedWidth);
            var expectedString = @$"{{""router"":""b827:ebff:fee1:e39a"",""muxs"":""{muxs}"",""uri"":""{(isHttps ? "wss" : "ws")}://localhost:1234{BasicsStationNetworkServer.DataEndpoint}/B8-27-EB-FF-FE-E1-E3-9A""}}";

            // act
            await this.lnsMessageProcessorMock.InternalHandleDiscoveryAsync(this.httpContextMock.Object, inputJsonString, this.socketMock.Object, CancellationToken.None);

            // assert
            Assert.Equal(expectedString, sentString);
        }

        [Fact]
        public async Task InternalHandleDataAsync_ShouldSendExpectedJsonResponseType_ForVersionMessage()
        {
            // arrange
            var inputJsonString = @"{""msgtype"": ""version"", ""station"": ""stationName"" }";
            var expectedSubstring = @"""msgtype"":""router_config""";
            InitializeConfigurationServiceMock();
            SetDataPathParameter();

            // intercepting the SendAsync to verify that what we sent is actually what we expected
            var sentString = string.Empty;
            this.socketMock.Setup(x => x.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                           .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((message, type, end, _) =>
                           {
                               sentString = Encoding.UTF8.GetString(message);
                               Assert.Equal(WebSocketMessageType.Text, type);
                               Assert.True(end);
                           });

            // act
            await this.lnsMessageProcessorMock.InternalHandleDataAsync(httpContextMock.Object,
                                                                       inputJsonString,
                                                                       this.socketMock.Object,
                                                                       CancellationToken.None);

            // assert
            Assert.Contains(expectedSubstring, sentString, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("dnmsg")]
        [InlineData("router_config")]
        public async Task InternalHandleDataAsync_ShouldThrow_OnNotExpectedMessageTypes(string msgtype)
        {
            // arrange
            var inputJsonString = $@"{{""msgtype"":""{msgtype}""}}";
            SetDataPathParameter();

            // act + assert
            await Assert.ThrowsAsync<NotSupportedException>(() => this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object,
                                                                                                                       inputJsonString,
                                                                                                                       this.socketMock.Object,
                                                                                                                       CancellationToken.None));
        }

        [Fact]
        public async Task InternalHandleDataAsync_Should_Rethrow_If_Value_Not_Present()
        {
            // arrange
            this.httpContextMock.SetupGet(h => h.Request).Returns(() => new DefaultHttpContext().Request);

            // act + assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object,
                                                                     string.Empty,
                                                                     this.socketMock.Object,
                                                                     CancellationToken.None));
        }

        private void InitializeConfigurationServiceMock() =>
            this.basicsStationConfigurationMock.Setup(bcs => bcs.GetRouterConfigMessageAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                               .Returns(Task.FromResult(@"{""msgtype"":""router_config"",...}"));

        private void SetDataPathParameter(StationEui stationEui = default) =>
            this.httpContextMock.SetupGet(h => h.Request).Returns(() =>
            {
                var httpContext = new DefaultHttpContext();
                httpContext.Request.RouteValues = new RouteValueDictionary
                {
                    [BasicsStationNetworkServer.RouterIdPathParameterName] = stationEui
                };
                return httpContext.Request;
            });
    }
}
