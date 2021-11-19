// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Moq;
    using Xunit;

    /// <summary>
    /// These tests test the integration between <see cref="LnsProtocolMessageProcessor"/> and <see cref="ConcentratorDeduplication"/>.
    /// </summary>
    public sealed class LnsProtocolMessageProcessorConcentratorDeduplicationIntegrationTests
        : IAsyncLifetime
    {
        private IHost testHost;

        public Task InitializeAsync() => Task.CompletedTask;

        private async Task CreateTestHostAsync(Mock<IMessageDispatcher> messageDispatcherMock)
        {
            this.testHost = await new HostBuilder()
                .ConfigureWebHost(builder =>
                    builder.UseTestServer()
                            .UseStartup<BasicsStationNetworkServerStartup>()
                            .ConfigureServices(services =>
                            {
                                var basicsStationConfigurationMock = new Mock<IBasicsStationConfigurationService>();
                                _ = basicsStationConfigurationMock.Setup(m => m.GetRegionAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(RegionManager.EU868));

                                _ = services.AddRouting();
                                _ = services.AddSingleton(basicsStationConfigurationMock.Object);
                                _ = services.AddSingleton(messageDispatcherMock.Object);
                            })
                            .UseEnvironment("Development"))
                .StartAsync();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_Same_Message_Comes_Multiple_Times_Result_Depends_On_Which_Concentrator_It_Was_Sent_From(bool isSameStation)
        {
            // arrange
            var dispatcherCounter = 0;
            var messageDispatcherMock = new Mock<IMessageDispatcher>();
            _ = messageDispatcherMock.Setup(x => x.DispatchRequest(It.IsAny<LoRaRequest>())).Callback(() => dispatcherCounter++);
            await CreateTestHostAsync(messageDispatcherMock);
            var wsClient = this.testHost.GetTestServer().CreateWebSocketClient();

            var station1 = StationEui.Parse("a8-27-eb-ff-fe-e1-e3-9a");
            var station2 = isSameStation ? station1 : StationEui.Parse("b8-27-eb-ff-fe-e1-e3-9a");
            var baseUri = $"localhost:5000{BasicsStationNetworkServer.DataEndpoint}";

            var wsUri1 = new UriBuilder($"{baseUri}/{station1}")
            {
                Scheme = "ws"
            }.Uri;
            var wsUri2 = new UriBuilder($"{baseUri}/{station2}")
            {
                Scheme = "ws"
            }.Uri;

            var message = @"{
                ""msgtype"": ""updf"",
                ""MHdr"": 128,
                ""DevAddr"": 58772467,
                ""FCtrl"": 0,
                ""FCnt"": 164,
                ""FOpts"": """",
                ""FPort"": 8,
                ""FRMPayload"": ""5ABBBA"",
                ""MIC"": -1943282916,
                ""RefTime"": 0.0,
                ""DR"": 4,
                ""Freq"": 868100000,
                ""upinfo"": {
                    ""rctx"": 0,
                    ""xtime"": 40250921680313459,
                    ""gpstime"": 0,
                    ""fts"": -1,
                    ""rssi"": -60,
                    ""snr"": 9,
                    ""rxtime"": 1635347491.917289
                }
            }";

            // act
            var socket1 = await wsClient.ConnectAsync(wsUri1, CancellationToken.None);
            var socket2 = await wsClient.ConnectAsync(wsUri2, CancellationToken.None);

            await socket1.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, default);
            await socket2.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, default);

            await socket1.CloseAsync(WebSocketCloseStatus.NormalClosure, "Byebye", default);
            await socket2.CloseAsync(WebSocketCloseStatus.NormalClosure, "Byebye", default);

            // assert
            var expected = isSameStation ? 2 : 1;
            Assert.Equal(expected, dispatcherCounter);
        }

        public async Task DisposeAsync()
        {
            await this.testHost?.StopAsync();
            this.testHost?.Dispose();
        }
    }
}
