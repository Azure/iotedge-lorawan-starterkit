// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.LoRaPhysical;
    using global::LoRaTools.Regions;
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
        private readonly Mock<IMessageDispatcher> messageDispatcher;
        private readonly Mock<IPacketForwarder> packetForwarder;
        private readonly LnsProtocolMessageProcessor lnsMessageProcessorMock;
        private readonly Mock<WebSocket> socketMock;
        private readonly Mock<HttpContext> httpContextMock;
        private readonly MockSequence receiveSequence = new();

        public LnsProtocolMessageProcessorTests()
        {
            var loggerMock = Mock.Of<ILogger<LnsProtocolMessageProcessor>>();
            this.socketMock = new Mock<WebSocket>();
            this.httpContextMock = new Mock<HttpContext>();
            this.basicsStationConfigurationMock = new Mock<IBasicsStationConfigurationService>();
            _ = this.basicsStationConfigurationMock.Setup(m => m.GetRegionAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                          .Returns(Task.FromResult(RegionManager.EU868));
            this.messageDispatcher = new Mock<IMessageDispatcher>();
            this.packetForwarder = new Mock<IPacketForwarder>();
            var upstreamDeduplicationMock = new Mock<IConcentratorDeduplication<UpstreamDataFrame>>();
            var joinRequestDeduplicationMock = new Mock<IConcentratorDeduplication<JoinRequestFrame>>();

            this.lnsMessageProcessorMock = new LnsProtocolMessageProcessor(this.basicsStationConfigurationMock.Object,
                                                                           new WebSocketWriterRegistry<StationEui, string>(Mock.Of<ILogger<WebSocketWriterRegistry<StationEui, string>>>(), null),
                                                                           this.packetForwarder.Object,
                                                                           this.messageDispatcher.Object,
                                                                           upstreamDeduplicationMock.Object,
                                                                           joinRequestDeduplicationMock.Object,
                                                                           loggerMock, new RegistryMetricTagBag(),
                                                                           // Do not pass meter since metric testing will be unreliable due to interference from test classes running in parallel.
                                                                           null);
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
            _ = SetupWebSocketConnection(isWebSocketRequest: false);

            // providing a mocked HttpResponse so that it's possible to verify stubbed properties
            InitializeHttpContextMockWithHttpResponse();

            // act
            await this.lnsMessageProcessorMock.ProcessIncomingRequestAsync(this.httpContextMock.Object,
                                                                           delegate { return Task.CompletedTask; },
                                                                           CancellationToken.None);

            // assert
            Assert.Equal(400, this.httpContextMock.Object.Response.StatusCode);
        }

        [Fact]
        public async Task ProcessIncomingRequestAsync_Should_Handle_OperationCanceledException()
        {
            // arrange
            _ = SetupWebSocketConnection(isWebSocketRequest: true);
            InitializeHttpContextMockWithHttpResponse();
            _ = this.socketMock.Setup(ws => ws.CloseAsync(WebSocketCloseStatus.NormalClosure, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                               .Throws(new OperationCanceledException("websocketexception", new WebSocketException(WebSocketError.ConnectionClosedPrematurely)));

            // act
            var ex =
                await Record.ExceptionAsync(() =>
                    this.lnsMessageProcessorMock.ProcessIncomingRequestAsync(this.httpContextMock.Object,
                                                                             delegate { return Task.CompletedTask; },
                                                                             CancellationToken.None));

            // assert
            Assert.Null(ex);
        }

        [Fact]
        public async Task ProcessIncomingRequestAsync_ShouldProcess_WebsocketRequests()
        {
            // arrange
            var httpContextMock = new Mock<HttpContext>();

            // mocking a websocket request
            var webSocketsManager = SetupWebSocketConnection(isWebSocketRequest: true);
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
            SetupSocketReceiveAsync(MessageFormat.Text, "test");
            httpContextMock.Setup(m => m.WebSockets).Returns(webSocketsManager.Object);

            // this is needed for logging the Basic Station (caller) remote ip address
            var connectionInfo = new Mock<ConnectionInfo>();
            connectionInfo.Setup(c => c.RemoteIpAddress).Returns(System.Net.IPAddress.Loopback);
            httpContextMock.Setup(m => m.Connection).Returns(connectionInfo.Object);

            // act and assert
            await this.lnsMessageProcessorMock.ProcessIncomingRequestAsync(httpContextMock.Object,
                                                                           delegate { return Task.CompletedTask; },
                                                                           CancellationToken.None);

            // assert that websocket is closed, as the input string was verified through local function handler
            Assert.Equal(WebSocketState.Closed, this.socketMock.Object.State);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, this.socketMock.Object.CloseStatus);
        }

        private Mock<WebSocketManager> SetupWebSocketConnection(bool isWebSocketRequest)
        {
            var webSocketsManager = new Mock<WebSocketManager>();
            _ = webSocketsManager.SetupGet(x => x.IsWebSocketRequest).Returns(isWebSocketRequest);
            _ = this.httpContextMock.SetupGet(m => m.WebSockets).Returns(webSocketsManager.Object);

            if (isWebSocketRequest)
            {
                _ = webSocketsManager.Setup(x => x.AcceptWebSocketAsync()).ReturnsAsync(this.socketMock.Object);
                _ = this.httpContextMock.Setup(x => x.Connection).Returns(new Mock<ConnectionInfo>().Object);
            }

            return webSocketsManager;
        }

        private void InitializeHttpContextMockWithHttpResponse()
        {
            var httpResponseMock = new Mock<HttpResponse>();
            _ = httpResponseMock.SetupAllProperties();
            _ = this.httpContextMock.Setup(m => m.Response).Returns(httpResponseMock.Object);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task InternalHandleDiscoveryAsync_ShouldSendProperJson(bool isHttps, bool isValidNic)
        {
            // arrange
            InitializeConfigurationServiceMock();

            // mocking localIpAddress
            var connectionInfoMock = new Mock<ConnectionInfo>();
            // this is going to select the network interface with most bytes received / sent
            // this should correspond to the real ethernet/wifi interface on the machine
            var firstNic = isValidNic ? NetworkInterface.GetAllNetworkInterfaces()
                                                        .OrderByDescending(x => x.GetIPv4Statistics().BytesReceived + x.GetIPv4Statistics().BytesSent)
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

            SetupSocketReceiveAsync(@"{ router: 'b827:ebff:fee1:e39a' }");

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
            await this.lnsMessageProcessorMock.InternalHandleDiscoveryAsync(this.httpContextMock.Object, this.socketMock.Object, CancellationToken.None);

            // assert
            Assert.Equal(expectedString, sentString);
        }

        [Fact]
        public async Task InternalHandleDataAsync_ShouldSendExpectedJsonResponseType_ForVersionMessage()
        {
            // arrange
            var expectedSubstring = @"""msgtype"":""router_config""";
            InitializeConfigurationServiceMock();
            SetDataPathParameter();

            SetupSocketReceiveAsync(@"{ msgtype: 'version', station: 'stationName' }");

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
            await this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
                                                                       this.socketMock.Object,
                                                                       CancellationToken.None);

            // assert
            Assert.Contains(expectedSubstring, sentString, StringComparison.Ordinal);
        }


        [Theory]
        [InlineData(LnsMessageType.ProprietaryDataFrame)]
        [InlineData(LnsMessageType.MulticastSchedule)]
        [InlineData(LnsMessageType.RemoteShell)]
        [InlineData(LnsMessageType.RunCommand)]
        [InlineData(LnsMessageType.TimeSync)]
        internal async Task InternalHandleDataAsync_ShouldNotThrow_OnNonHandledMessageTypes(LnsMessageType lnsMessageType)
        {
            // arrange
            InitializeConfigurationServiceMock();
            SetDataPathParameter();

            SetupSocketReceiveAsync($@"{{ msgtype: '{lnsMessageType.ToBasicStationString()}' }}");

            // act and assert
            // (it's important that it does not throw)
            await this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
                                                                       this.socketMock.Object,
                                                                       CancellationToken.None);
        }

        private static Rxpk GetExpectedRxpk()
        {
            var radioMetadataUpInfo = new RadioMetadataUpInfo(0, 68116944405337035, 0, -53, (float)8.25);
            var radioMetadata = new RadioMetadata(new DataRate(5), new Hertz(868300000), radioMetadataUpInfo);
            return new BasicStationToRxpk(radioMetadata, RegionManager.EU868);
        }

        private static bool AreRxpkEqual(Rxpk subject, Rxpk other) =>
            subject.Chan == other.Chan
            && subject.Codr == other.Codr
            && subject.Data == other.Data
            && subject.Datr == other.Datr
            && subject.Freq == other.Freq
            && subject.Lsnr == other.Lsnr
            && subject.Modu == other.Modu
            && subject.RequiredSnr == other.RequiredSnr
            && subject.Rfch == other.Rfch
            && subject.Rssi == other.Rssi
            && subject.Size == other.Size
            && subject.Stat == other.Stat
            && subject.Time == other.Time
            && subject.Tmms == other.Tmms
            && subject.Tmst == other.Tmst;

        [Fact]
        public async Task InternalHandleDataAsync_ShouldProperlyCreateLoraPayloadForUpdfRequest()
        {
            // arrange
            var message = JsonUtil.Strictify(@"{'msgtype':'updf','MHdr':128,'DevAddr':50244358,'FCtrl':0,'FCnt':1,'FOpts':'','FPort':8,'FRMPayload':'CB',
                                                'MIC':45234788,'RefTime':0.000000,'DR':5,'Freq':868300000,'upinfo':{'rctx':0,'xtime':68116944405337035,
                                                'gpstime':0,'fts':-1,'rssi':-53,'snr':8.25,'rxtime':1636131701.731686}}");
            var expectedRxpk = GetExpectedRxpk();
            var expectedMhdr = new byte[] { 128 };
            var expectedDevAddr = new byte[] { 2, 254, 171, 6 };
            var expectedMic = new byte[] { 100, 58, 178, 2 };
            SetDataPathParameter();
            SetupSocketReceiveAsync(message);

            // intercepting messageDispatcher
            LoRaRequest loRaRequest = null;
            this.messageDispatcher.Setup(m => m.DispatchRequest(It.IsAny<LoRaRequest>()))
                                  .Callback<LoRaRequest>((req) => loRaRequest = req);

            // act
            await this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
                                                                       this.socketMock.Object,
                                                                       CancellationToken.None);

            // assert
            Assert.NotNull(loRaRequest);
            Assert.True(AreRxpkEqual(loRaRequest.Rxpk, expectedRxpk));
            Assert.Equal(expectedDevAddr, loRaRequest.Payload.DevAddr.Span.ToArray());
            Assert.Equal(LoRaMessageType.ConfirmedDataUp, loRaRequest.Payload.LoRaMessageType);
            Assert.Equal(expectedMhdr, loRaRequest.Payload.Mhdr.Span.ToArray());
            Assert.Equal(expectedMic, loRaRequest.Payload.Mic.Span.ToArray());
            Assert.Equal(packetForwarder.Object, loRaRequest.PacketForwarder);
            Assert.Equal(RegionManager.EU868, loRaRequest.Region);
        }

        [Fact]
        public async Task InternalHandleDataAsync_ShouldProperlyCreateLoraPayloadForJoinRequest()
        {
            // arrange
            var message = JsonUtil.Strictify(@"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E',
                                                'DevNonce':54360,'MIC':-1056607131,'RefTime':0.000000,'DR':5,'Freq':868300000,'upinfo':{'rctx':0,
                                                'xtime':68116944405337035,'gpstime':0,'fts':-1,'rssi':-53,'snr':8.25,'rxtime':1636131701.731686}}");
            var expectedRxpk = GetExpectedRxpk();
            var expectedMhdr = new byte[] { 0 };
            var expectedMic = new byte[] { 101, 116, 5, 193 };
            var expectedAppEui = new byte[] { 181, 196, 210, 229, 200, 120, 98, 71 };
            var expectedDevEui = new byte[] { 158, 22, 164, 238, 223, 193, 39, 133 };
            var expectedDevNonce = new byte[] { 88, 212 };
            SetDataPathParameter();
            SetupSocketReceiveAsync(message);

            // intercepting messageDispatcher
            LoRaRequest loRaRequest = null;
            this.messageDispatcher.Setup(m => m.DispatchRequest(It.IsAny<LoRaRequest>()))
                                  .Callback<LoRaRequest>((req) => loRaRequest = req);

            // act
            await this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
                                                                       this.socketMock.Object,
                                                                       CancellationToken.None);

            // assert
            Assert.NotNull(loRaRequest);
            Assert.True(AreRxpkEqual(loRaRequest.Rxpk, expectedRxpk));
            Assert.IsType<LoRaPayloadJoinRequestLns>(loRaRequest.Payload);
            Assert.Equal(LoRaMessageType.JoinRequest, loRaRequest.Payload.LoRaMessageType);
            Assert.Equal(expectedMhdr, loRaRequest.Payload.Mhdr.Span.ToArray());
            Assert.Equal(expectedMic, loRaRequest.Payload.Mic.Span.ToArray());
            Assert.Equal(expectedAppEui, ((LoRaPayloadJoinRequestLns)loRaRequest.Payload).AppEUI.Span.ToArray());
            Assert.Equal(expectedDevEui, ((LoRaPayloadJoinRequestLns)loRaRequest.Payload).DevEUI.Span.ToArray());
            Assert.Equal(expectedDevNonce, ((LoRaPayloadJoinRequestLns)loRaRequest.Payload).DevNonce.Span.ToArray());
            Assert.Equal(packetForwarder.Object, loRaRequest.PacketForwarder);
            Assert.Equal(RegionManager.EU868, loRaRequest.Region);
        }

        [Theory]
        [InlineData("dnmsg")]
        [InlineData("router_config")]
        public async Task InternalHandleDataAsync_ShouldThrow_OnNotExpectedMessageTypes(string msgtype)
        {
            // arrange
            SetDataPathParameter();
            SetupSocketReceiveAsync($@"{{ msgtype: '{msgtype}' }}");

            // act + assert
            await Assert.ThrowsAsync<NotSupportedException>(() => this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
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
                this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
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

        private enum MessageFormat { Json, Text };

        private void SetupSocketReceiveAsync(params string[] jsonMessages) =>
            SetupSocketReceiveAsync(MessageFormat.Json, jsonMessages);

        private void SetupSocketReceiveAsync(MessageFormat format, params string[] messages)
        {
            foreach (var bytes in from m in messages
                                  select format == MessageFormat.Json ? JsonUtil.Strictify(m) : m into m
                                  select Encoding.UTF8.GetBytes(m))
            {
                this.socketMock
                    .InSequence(receiveSequence)
                    .Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                    .Callback<Memory<byte>, CancellationToken>((destination, _) => bytes.CopyTo(destination))
                    .ReturnsAsync(new ValueWebSocketReceiveResult(bytes.Length, WebSocketMessageType.Text, true));
            }

            this.socketMock
                .InSequence(receiveSequence)
                .Setup(x => x.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }
    }
}
