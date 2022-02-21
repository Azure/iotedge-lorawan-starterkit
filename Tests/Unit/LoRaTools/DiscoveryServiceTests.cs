// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.LoRaTools
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.NetworkServerDiscovery;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class DiscoveryServiceTests
    {
        private readonly Mock<ILnsDiscovery> lnsDiscoveryMock;
        private readonly DiscoveryService subject;

        public DiscoveryServiceTests()
        {
            this.lnsDiscoveryMock = new Mock<ILnsDiscovery>();
            this.subject = new DiscoveryService(this.lnsDiscoveryMock.Object, NullLogger<DiscoveryService>.Instance);
        }

        [Fact]
        public async Task HandleDiscoveryRequestAsync_Should_Return_400_If_Request_Is_Not_WebSocket()
        {
            // arrange
            var (httpContext, _) = SetupWebSocketConnection(notWebSocketRequest: true);

            // act
            await this.subject.HandleDiscoveryRequestAsync(httpContext.Object, CancellationToken.None);

            // assert
            Assert.Equal(400, httpContext.Object.Response.StatusCode);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task HandleDiscoveryRequestAsync_Success_Response(bool isHttps, bool isValidNic)
        {
            // arrange
            using var cts = new CancellationTokenSource();
            var stationEui = new StationEui(ulong.MaxValue);
            var lnsUri = new Uri($"{(isHttps ? "wss" : "ws")}://localhost:5000");

            var (httpContextMock, webSocketMock) = SetupWebSocketConnection(isHttps: isHttps);

            // setup discovery request
            SetupDiscoveryRequest(webSocketMock, stationEui);

            // setup lns resolution
            this.lnsDiscoveryMock.Setup(d => d.ResolveLnsAsync(It.IsAny<StationEui>(), cts.Token))
                                 .ReturnsAsync(lnsUri);

            // setup muxs info
            var connectionInfoMock = new Mock<ConnectionInfo>();
            _ = httpContextMock.Setup(h => h.Connection).Returns(connectionInfoMock.Object);
            var nic = isValidNic ? GetMostUsedNic() : null;
            var ip = isValidNic ? nic?.GetIPProperties().UnicastAddresses.First().Address : new IPAddress(new byte[] { 192, 168, 1, 10 });
            _ = connectionInfoMock.SetupGet(ci => ci.LocalIpAddress).Returns(ip);
            var muxs = Id6.Format(nic?.GetPhysicalAddress().Convert48To64() ?? 0, Id6.FormatOptions.FixedWidth);

            // capture sent message
            var actualResponse = string.Empty;
            webSocketMock.Setup(x => x.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                         .Callback((ArraySegment<byte> message, WebSocketMessageType _, bool _, CancellationToken _) =>
                             actualResponse = Encoding.UTF8.GetString(message));

            // act
            await this.subject.HandleDiscoveryRequestAsync(httpContextMock.Object, cts.Token);

            // assert
            var expectedResponse = @$"{{""router"":""{stationEui:i}"",""muxs"":""{muxs}"",""uri"":""{(isHttps ? "wss" : "ws")}://{lnsUri.Host}:{lnsUri.Port}/router-data/{stationEui.ToHex()}""}}";
            Assert.Equal(expectedResponse, actualResponse);
            webSocketMock.Verify(ws => ws.SendAsync(It.IsAny<ArraySegment<byte>>(),
                                                    WebSocketMessageType.Text,
                                                    true, cts.Token), Times.Once);
        }

        [Fact]
        public async Task HandleDiscoveryRequestAsync_Responds_With_Error_Message()
        {
            // arrange
            using var cts = new CancellationTokenSource();
            var (httpContextMock, webSocketMock) = SetupWebSocketConnection();
            const string errorMessage = "LBS is not registered in IoT Hub";

            SetupDiscoveryRequest(webSocketMock, new StationEui(1));
            this.lnsDiscoveryMock.Setup(d => d.ResolveLnsAsync(It.IsAny<StationEui>(), cts.Token))
                                 .ThrowsAsync(new InvalidOperationException(errorMessage));

            // capture sent message
            var actualResponse = string.Empty;
            webSocketMock.Setup(x => x.SendAsync(It.IsAny<ArraySegment<byte>>(), It.IsAny<WebSocketMessageType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                         .Callback((ArraySegment<byte> message, WebSocketMessageType _, bool _, CancellationToken _) =>
                             actualResponse = Encoding.UTF8.GetString(message));

            // act + assert
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => this.subject.HandleDiscoveryRequestAsync(httpContextMock.Object, cts.Token));
            Assert.Contains(errorMessage, actualResponse, StringComparison.Ordinal);
            webSocketMock.Verify(ws => ws.SendAsync(It.IsAny<ArraySegment<byte>>(), WebSocketMessageType.Text,
                                                    true, cts.Token), Times.Once);
        }

        public static TheoryData<Uri> HandleDiscoveryRequestAsync_Detects_Uri_Misconfiguration_TheoryData() => TheoryDataFactory.From(new[]
        {
            new Uri("mylns", UriKind.Relative),
            new Uri("http://mylns:1234")
        });

        [Theory]
        [MemberData(nameof(HandleDiscoveryRequestAsync_Detects_Uri_Misconfiguration_TheoryData))]
        public async Task HandleDiscoveryRequestAsync_Detects_Uri_Misconfiguration(Uri hostAddress)
        {
            // arrange
            var (httpContextMock, webSocketMock) = SetupWebSocketConnection();

            SetupDiscoveryRequest(webSocketMock, new StationEui(1));
            this.lnsDiscoveryMock.Setup(d => d.ResolveLnsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                 .ReturnsAsync(hostAddress);

            // act + assert
            var ex = await Assert.ThrowsAsync<LoRaProcessingException>(() => this.subject.HandleDiscoveryRequestAsync(httpContextMock.Object, CancellationToken.None));
            Assert.Equal(LoRaProcessingErrorCode.InvalidDeviceConfiguration, ex.ErrorCode);
        }

        private static void SetupDiscoveryRequest(Mock<WebSocket> webSocketMock, StationEui stationEui)
        {
            var discvoryMessage = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { router = stationEui.AsUInt64 }));
            webSocketMock.Setup(ws => ws.ReceiveAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>()))
                         .Callback((Memory<byte> destination, CancellationToken _) => discvoryMessage.CopyTo(destination))
                         .ReturnsAsync(new ValueWebSocketReceiveResult(discvoryMessage.Length, WebSocketMessageType.Text, true));
        }

        /// <summary>
        /// Selects the network interface with most bytes received/sent.
        /// Should correspond to the real ethernet/wifi interface on the machine.
        /// </summary>
        internal static NetworkInterface? GetMostUsedNic() =>
            NetworkInterface.GetAllNetworkInterfaces()
                            .OrderByDescending(x => x.GetIPv4Statistics().BytesReceived + x.GetIPv4Statistics().BytesSent)
                            .FirstOrDefault();


        private static (Mock<HttpContext>, Mock<WebSocket>) SetupWebSocketConnection(bool isHttps = false, bool notWebSocketRequest = false)
        {
            var socketMock = new Mock<WebSocket>();

            var webSocketsManager = new Mock<WebSocketManager>();
            _ = webSocketsManager.SetupGet(wsm => wsm.IsWebSocketRequest).Returns(!notWebSocketRequest);
            _ = webSocketsManager.Setup(wsm => wsm.AcceptWebSocketAsync()).ReturnsAsync(socketMock.Object);

            var httpRequestMock = new Mock<HttpRequest>();
            _ = httpRequestMock.SetupGet(ci => ci.IsHttps).Returns(isHttps);

            var httpContextMock = new Mock<HttpContext>();
            _ = httpContextMock.SetupGet(h => h.WebSockets).Returns(webSocketsManager.Object);
            _ = httpContextMock.SetupGet(h => h.Response).Returns(Mock.Of<HttpResponse>());
            _ = httpContextMock.SetupGet(h => h.Request).Returns(httpRequestMock.Object);
            _ = httpContextMock.SetupGet(h => h.Connection).Returns(Mock.Of<ConnectionInfo>());

            return (httpContextMock, socketMock);
        }
    }
}
