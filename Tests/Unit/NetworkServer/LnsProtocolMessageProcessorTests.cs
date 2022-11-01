// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
    using global::LoRaTools.LoRaMessage;
    using global::LoRaTools.NetworkServerDiscovery;
    using global::LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using LoRaWan.Tests.Unit.LoRaTools;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class LnsProtocolMessageProcessorTests
    {
        private readonly Mock<IBasicsStationConfigurationService> basicsStationConfigurationMock;
        private readonly Mock<IMessageDispatcher> messageDispatcher;
        private readonly Mock<IDownstreamMessageSender> downstreamMessageSender;
        private readonly Mock<ITracing> tracingMock;
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
            this.downstreamMessageSender = new Mock<IDownstreamMessageSender>();
            this.tracingMock = new Mock<ITracing>();

            this.lnsMessageProcessorMock = new LnsProtocolMessageProcessor(this.basicsStationConfigurationMock.Object,
                                                                           new WebSocketWriterRegistry<StationEui, string>(Mock.Of<ILogger<WebSocketWriterRegistry<StationEui, string>>>(), null),
                                                                           this.downstreamMessageSender.Object,
                                                                           this.messageDispatcher.Object,
                                                                           NullLoggerFactory.Instance,
                                                                           loggerMock,
                                                                           new RegistryMetricTagBag(new NetworkServerConfiguration { GatewayID = "foogateway" }),
                                                                           // Do not pass meter since metric testing will be unreliable due to interference from test classes running in parallel.
                                                                           null,
                                                                           this.tracingMock.Object);
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

        /// <summary>
        /// Ensures that a WebSocket connection can (or cannot) be established from the instance's <see cref="HttpContext"/>.
        /// </summary>
        private Mock<WebSocketManager> SetupWebSocketConnection(bool notAWebSocketRequest = false)
        {
            var isWebSocketRequest = !notAWebSocketRequest;
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

        [Theory]
        [InlineData(true, true, 1234)]
        [InlineData(false, false, 1234)]
        [InlineData(true, false, 1234)]
        [InlineData(false, true, 1234)]
        [InlineData(true, true, null)]
        [InlineData(false, true, null)]
        public async Task InternalHandleDiscoveryAsync_ShouldSendProperJson(bool isHttps, bool isValidNic, int? port)
        {
            // arrange
            const string host = "localhost";
            InitializeConfigurationServiceMock();

            // mocking localIpAddress
            var connectionInfoMock = new Mock<ConnectionInfo>();
            var nic = isValidNic ? DiscoveryServiceTests.GetMostUsedNic() : null;
            var ip = isValidNic ? nic?.GetIPProperties().UnicastAddresses.First().Address : new IPAddress(new byte[] { 192, 168, 1, 10 });
            _ = connectionInfoMock.SetupGet(ci => ci.LocalIpAddress).Returns(ip);
            this.httpContextMock.Setup(h => h.Connection).Returns(connectionInfoMock.Object);

            var mockHttpRequest = new Mock<HttpRequest>();
            mockHttpRequest.SetupGet(x => x.Host).Returns(port is { } somePort ? new HostString(host, somePort) : new HostString(host));
            mockHttpRequest.SetupGet(x => x.IsHttps).Returns(isHttps);
            mockHttpRequest.SetupGet(x => x.Scheme).Returns("ws");
            this.httpContextMock.Setup(h => h.Request).Returns(mockHttpRequest.Object);
            var webSocketManager = new Mock<WebSocketManager>();
            webSocketManager.Setup(wsm => wsm.AcceptWebSocketAsync()).ReturnsAsync(this.socketMock.Object);
            webSocketManager.Setup(wsm => wsm.IsWebSocketRequest).Returns(true);
            this.httpContextMock.Setup(h => h.WebSockets).Returns(webSocketManager.Object);

            SetupSocketReceiveAsync(@"{ router: 'b827:ebff:fee1:e39a' }");

            // intercepting the SendAsync to verify that what we sent is actually what we expected
            var sentString = string.Empty;
            WebSocketMessageType? sentType = null;
            bool? sentEnd = null;
            this.socketMock.Setup(x => x.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                           .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((message, type, end, _) =>
                           {
                               sentString = Encoding.UTF8.GetString(message);
                               sentType = type;
                               sentEnd = end;
                           });

            var muxs = Id6.Format(nic?.GetPhysicalAddress().Convert48To64() ?? 0, Id6.FormatOptions.FixedWidth);
            var portString = port is { } somePortPrime ? $":{somePortPrime}" : string.Empty;
            var expectedString = @$"{{""router"":""b827:ebff:fee1:e39a"",""muxs"":""{muxs}"",""uri"":""{(isHttps ? "wss" : "ws")}://{host}{portString}{BasicsStationNetworkServer.DataEndpoint}/B827EBFFFEE1E39A""}}";

            // act
            await this.lnsMessageProcessorMock.HandleDiscoveryAsync(this.httpContextMock.Object, CancellationToken.None);

            // assert
            Assert.Equal(expectedString, sentString);
            Assert.Equal(WebSocketMessageType.Text, sentType.Value);
            Assert.True(sentEnd.Value);
        }

        [Fact]
        public async Task InternalHandleDataAsync_ShouldSendExpectedJsonResponseType_ForVersionMessage()
        {
            // arrange
            var expectedSubstring = @"""msgtype"":""router_config""";
            InitializeConfigurationServiceMock();
            SetDataPathParameter();

            SetupSocketReceiveAsync(@"{ msgtype: 'version', station: 'stationName', package: '1.0.0' }");

            // intercepting the SendAsync to verify that what we sent is actually what we expected
            var sentString = string.Empty;
            WebSocketMessageType? sentType = null;
            bool? sentEnd = null;
            this.socketMock.Setup(x => x.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                           .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((message, type, end, _) =>
                           {
                               sentString = Encoding.UTF8.GetString(message);
                               sentType = type;
                               sentEnd = end;
                           });

            // act
            await this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
                                                                       this.socketMock.Object,
                                                                       CancellationToken.None);

            // assert
            Assert.NotNull(sentType);
            Assert.NotNull(sentEnd);
            Assert.Contains(expectedSubstring, sentString, StringComparison.Ordinal);
            Assert.Equal(WebSocketMessageType.Text, sentType.Value);
            Assert.True(sentEnd.Value);
        }

        [Fact]
        public async Task InternalHandleDataAsync_ShouldSendExpectedJsonResponseType_ForTimeSyncMessage()
        {
            // arrange
            var receievedMsgType = "timesync";
            ulong receivedTxTime = 1023024197;
            var minimumExpectedGpsTime = (ulong)DateTime.UtcNow.AddMinutes(-10)
                .Subtract(LnsProtocolMessageProcessor.GpsEpoch).TotalMilliseconds * 1000;
            var maximumExpectedGpsTime = (ulong)DateTime.UtcNow.AddMinutes(10)
                .Subtract(LnsProtocolMessageProcessor.GpsEpoch).TotalMilliseconds * 1000;

            InitializeConfigurationServiceMock();
            SetDataPathParameter();

            SetupSocketReceiveAsync("{ msgtype: '" + receievedMsgType + "', txtime: " + receivedTxTime + "}");

            // intercepting the SendAsync to verify that what we sent is actually what we expected
            var sentString = string.Empty;
            WebSocketMessageType? sentType = null;
            bool? sentEnd = null;
            this.socketMock.Setup(x => x.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                           .Callback<ArraySegment<byte>, WebSocketMessageType, bool, CancellationToken>((message, type, end, _) =>
                           {
                               sentString = Encoding.UTF8.GetString(message);
                               sentType = type;
                               sentEnd = end;
                           });

            // act
            await this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
                                                                       this.socketMock.Object,
                                                                       CancellationToken.None);

            // assert
            var sentJson = JsonSerializer.Deserialize<TimeSyncMessage>(sentString);
            Assert.Equal(sentJson.MsgType, receievedMsgType);
            Assert.Equal(sentJson.TxTime, receivedTxTime);
            Assert.True(sentJson.GpsTime > minimumExpectedGpsTime);
            Assert.True(sentJson.GpsTime < maximumExpectedGpsTime);
            Assert.Equal(WebSocketMessageType.Text, sentType.Value);
            Assert.True(sentEnd.Value);
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

        private static RadioMetadata GetExpectedRadioMetadata()
        {
            var radioMetadataUpInfo = new RadioMetadataUpInfo(0, 68116944405337035, 0, -53, 8.25f);
            return new RadioMetadata(DataRateIndex.DR5, new Hertz(868300000), radioMetadataUpInfo);
        }

        [Fact]
        public async Task HandleDataAsync_ShouldProperlyCreateLoraPayloadForUpdfRequest()
        {
            // arrange
            var message = JsonUtil.Strictify(@"{'msgtype':'updf','MHdr':128,'DevAddr':50244358,'FCtrl':0,'FCnt':1,'FOpts':'','FPort':8,'FRMPayload':'CB',
                                                'MIC':45234788,'RefTime':0.000000,'DR':5,'Freq':868300000,'upinfo':{'rctx':0,'xtime':68116944405337035,
                                                'gpstime':0,'fts':-1,'rssi':-53,'snr':8.25,'rxtime':1636131701.731686}}");
            var expectedRadioMetadata = GetExpectedRadioMetadata();
            var expectedMhdr = new MacHeader(MacMessageType.ConfirmedDataUp);
            var expectedDevAddr = new DevAddr(50244358);
            var expectedMic = Mic.Read(new byte[] { 100, 58, 178, 2 });
            SetDataPathParameter();
            SetupSocketReceiveAsync(message);
            _ = SetupWebSocketConnection();

            // intercepting messageDispatcher
            LoRaRequest loRaRequest = null;
            this.messageDispatcher.Setup(m => m.DispatchRequest(It.IsAny<LoRaRequest>()))
                                  .Callback<LoRaRequest>((req) => loRaRequest = req);

            // act
            await this.lnsMessageProcessorMock.HandleDataAsync(this.httpContextMock.Object, CancellationToken.None);

            // assert
            Assert.NotNull(loRaRequest);
            Assert.Equal(loRaRequest.RadioMetadata, expectedRadioMetadata);
            Assert.Equal(expectedDevAddr, loRaRequest.Payload.DevAddr);
            Assert.Equal(MacMessageType.ConfirmedDataUp, loRaRequest.Payload.MessageType);
            Assert.Equal(expectedMhdr, loRaRequest.Payload.MHdr);
            Assert.Equal(expectedMic, loRaRequest.Payload.Mic);
            Assert.Equal(downstreamMessageSender.Object, loRaRequest.DownstreamMessageSender);
            Assert.Equal(RegionManager.EU868, loRaRequest.Region);
        }

        [Fact]
        public async Task HandleDataAsync_ShouldProperlyCreateLoraPayloadForJoinRequest()
        {
            // arrange
            var message = JsonUtil.Strictify(@"{'msgtype':'jreq','MHdr':0,'JoinEui':'47-62-78-C8-E5-D2-C4-B5','DevEui':'85-27-C1-DF-EE-A4-16-9E',
                                                'DevNonce':54360,'MIC':-1056607131,'RefTime':0.000000,'DR':5,'Freq':868300000,'upinfo':{'rctx':0,
                                                'xtime':68116944405337035,'gpstime':0,'fts':-1,'rssi':-53,'snr':8.25,'rxtime':1636131701.731686}}");
            var expectedRadioMetadata = GetExpectedRadioMetadata();
            var expectedMhdr = new MacHeader(MacMessageType.JoinRequest);
            var expectedMic = Mic.Read(new byte[] { 101, 116, 5, 193 });
            var expectedJoinEui = JoinEui.Read(new byte[] { 181, 196, 210, 229, 200, 120, 98, 71 });
            var expectedDevEui = DevEui.Read(new byte[] { 158, 22, 164, 238, 223, 193, 39, 133 });
            var expectedDevNonce = DevNonce.Read(new byte[] { 88, 212 });
            SetDataPathParameter();
            SetupSocketReceiveAsync(message);
            _ = SetupWebSocketConnection();

            // intercepting messageDispatcher
            LoRaRequest loRaRequest = null;
            this.messageDispatcher.Setup(m => m.DispatchRequest(It.IsAny<LoRaRequest>()))
                                  .Callback<LoRaRequest>((req) => loRaRequest = req);

            // act
            await this.lnsMessageProcessorMock.HandleDataAsync(this.httpContextMock.Object, CancellationToken.None);

            // assert
            Assert.NotNull(loRaRequest);
            Assert.Equal(loRaRequest.RadioMetadata, expectedRadioMetadata);
            Assert.IsType<LoRaPayloadJoinRequest>(loRaRequest.Payload);
            Assert.Equal(MacMessageType.JoinRequest, loRaRequest.Payload.MessageType);
            Assert.Equal(expectedMhdr, loRaRequest.Payload.MHdr);
            Assert.Equal(expectedMic, loRaRequest.Payload.Mic);
            Assert.Equal(expectedJoinEui, ((LoRaPayloadJoinRequest)loRaRequest.Payload).AppEui);
            Assert.Equal(expectedDevEui, ((LoRaPayloadJoinRequest)loRaRequest.Payload).DevEUI);
            Assert.Equal(expectedDevNonce, ((LoRaPayloadJoinRequest)loRaRequest.Payload).DevNonce);
            Assert.Equal(downstreamMessageSender.Object, loRaRequest.DownstreamMessageSender);
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
        public async Task InternalHandleDataAsync_Starts_And_Stops_Message_Tracing()
        {
            // arrange
            var disposableMock = new Mock<IDisposable>();
            this.tracingMock.Setup(t => t.TrackDataMessage()).Returns(disposableMock.Object);
            SetDataPathParameter();
            InitializeConfigurationServiceMock();
            SetupSocketReceiveAsync($@"{{ msgtype: '{LnsMessageType.Version.ToBasicStationString()}', station: 'stationName', package: '1.0.0' }}");

            // act
            await this.lnsMessageProcessorMock.InternalHandleDataAsync(this.httpContextMock.Object.Request.RouteValues,
                                                                       this.socketMock.Object,
                                                                       CancellationToken.None);

            // assert
            this.tracingMock.Verify(t => t.TrackDataMessage(), Times.Once);
            disposableMock.Verify(t => t.Dispose(), Times.Once);
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
