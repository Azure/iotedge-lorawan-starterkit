// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Common;
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
        private const string TestDataFrame =
            @"{msgtype: 'updf', MHdr: 128, DevAddr: 58772467, FCtrl: 0, FCnt: 164, FOpts: '', FPort: 8, FRMPayload: '5ABBBA', MIC: -1943282916, RefTime: 0.0, DR: 4, Freq: 868100000, upinfo: {rctx: 0, xtime: 40250921680313459, gpstime: 0, fts: -1, rssi: -60, snr: 9, rxtime: 1635347491.917289}}";
        //private const string TestJoinRequest =
        //    @"{msgtype: 'jreq', MHdr:0, JoinEui: '47-62-78-C8-E5-D2-C4-B5', DevEui:'85-27-C1-DF-EE-A4-16-9E', DevNonce: 54360, MIC: -1056607131, RefTime: 0.000000, DR: 5, Freq: 868500000, upinfo: { rctx: 0, xtime: 68116944372333395, gpstime: 0, fts: -1, rssi: -54, snr: 7.25, rxtime: 1636131668.725738}}";

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
        [InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", TestDataFrame, 2)]
        //[InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", TestDataFrame, 1)]
        //[InlineData("11-11-11-11-11-11-11-11", "11-11-11-11-11-11-11-11", TestJoinRequest, 2)]
        //[InlineData("11-11-11-11-11-11-11-11", "22-22-22-22-22-22-22-22", TestJoinRequest, 1)]
        public async Task When_Same_Message_Comes_Multiple_Times_Result_Depends_On_Which_Concentrator_It_Was_Sent_From(string station1, string station2, string message, int expectedMessagesUpstream)
        {
            // arrange
            var dispatcherCounter = 0;
            var messageDispatcherMock = new Mock<IMessageDispatcher>();
            _ = messageDispatcherMock.Setup(x => x.DispatchRequest(It.IsAny<LoRaRequest>())).Callback(() => dispatcherCounter++);
            await CreateTestHostAsync(messageDispatcherMock);
            var wsClient = this.testHost.GetTestServer().CreateWebSocketClient();

            var baseUri = new Uri(new Uri("ws://localhost:5000/"), BasicsStationNetworkServer.DataEndpoint + "/");
            var wsUri1 = new Uri(baseUri, station1);
            var wsUri2 = new Uri(baseUri, station2);

            // act
            var validJsonMessage = JsonUtil.Strictify(message);
            using var socket1 = await wsClient.ConnectAsync(wsUri1, CancellationToken.None);
            using var socket2 = await wsClient.ConnectAsync(wsUri2, CancellationToken.None);

            await socket1.SendAsync(Encoding.UTF8.GetBytes(validJsonMessage), WebSocketMessageType.Text, true, default);
            await socket2.SendAsync(Encoding.UTF8.GetBytes(validJsonMessage), WebSocketMessageType.Text, true, default);

            await Task.Delay(10); // adding a small delay to ensure messages are sent before closing the socket
            await socket1.CloseAsync(WebSocketCloseStatus.NormalClosure, "Byebye", default);
            await socket2.CloseAsync(WebSocketCloseStatus.NormalClosure, "Byebye", default);

            // assert
            Assert.Equal(expectedMessagesUpstream, dispatcherCounter);
        }

        public async Task DisposeAsync()
        {
            await this.testHost?.StopAsync();
            this.testHost?.Dispose();
        }
    }
}
