// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan.NetworkServerDiscovery;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    internal sealed class LnsDiscoveryApplication : WebApplicationFactory<Program>
    {
        public Mock<RegistryManager>? RegistryManagerMock { get; private set; }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                RegistryManagerMock = new Mock<RegistryManager>();
                services.RemoveAll<ILnsDiscovery>();
                services.AddSingleton<ILnsDiscovery>(sp => new TagBasedLnsDiscovery(sp.GetRequiredService<IMemoryCache>(), RegistryManagerMock.Object, sp.GetRequiredService<ILogger<TagBasedLnsDiscovery>>()));
            });

            return base.CreateHost(builder);
        }
    }

    public sealed class LnsDiscoveryIntegrationTests : IDisposable
    {
        private readonly LnsDiscoveryApplication subject;

        public LnsDiscoveryIntegrationTests()
        {
            this.subject = new LnsDiscoveryApplication();
        }

        [Fact]
        public async Task RouterInfo_Returns_Lns()
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            var stationEui = new StationEui(1);
            var hostAddresses = new[] { "ws://foo:5000", "wss://bar:5001" };
            var client = this.subject.Server.CreateWebSocketClient();
            var webSocket = await client.ConnectAsync(new Uri(this.subject.Server.BaseAddress, "router-info"), cancellationToken);
            SetupIotHubResponse(stationEui, hostAddresses);

            // act
            var result = await SendMessageAsync(webSocket, new { router = stationEui.AsUInt64 }, cancellationToken);

            // assert
            AssertContainsHostAddress(new Uri(hostAddresses[0]), stationEui, result);
        }

        [Fact]
        public async Task RouterInfo_Returns_Lns_Using_Round_Robin_On_Rerequest()
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            var stationEui = new StationEui(1);
            var hostAddresses = new[] { "ws://foo:5000", "wss://bar:5001" };
            var client = this.subject.Server.CreateWebSocketClient() ?? throw new InvalidOperationException("Could not create client.");
            SetupIotHubResponse(stationEui, hostAddresses);

            // act
            var first = await ExecuteAsync();
            var second = await ExecuteAsync();
            var third = await ExecuteAsync();
            Task<string> ExecuteAsync() => SendSingleMessageAsync(client, stationEui, cancellationToken);

            // assert
            AssertContainsHostAddress(new Uri(hostAddresses[0]), stationEui, first);
            AssertContainsHostAddress(new Uri(hostAddresses[1]), stationEui, second);
            AssertContainsHostAddress(new Uri(hostAddresses[0]), stationEui, third);
        }

        [Fact]
        public async Task RouterInfo_Returns_Same_Lns_For_Different_Stations()
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            var firstStation = new StationEui(1);
            var secondStation = new StationEui(2);
            var hostAddresses = new[] { "ws://foo:5000", "wss://bar:5001" };
            var client = this.subject.Server.CreateWebSocketClient();
            SetupIotHubResponse(firstStation, hostAddresses);
            SetupIotHubResponse(secondStation, hostAddresses);

            // act
            var firstResult = await SendSingleMessageAsync(client, firstStation, cancellationToken);
            var secondResult = await SendSingleMessageAsync(client, secondStation, cancellationToken);

            // assert
            AssertContainsHostAddress(new Uri(hostAddresses[0]), firstStation, firstResult);
            AssertContainsHostAddress(new Uri(hostAddresses[0]), secondStation, secondResult);
        }

        [Fact]
        public async Task RouterInfo_Returns_400_If_Connection_Is_Not_WebSocket()
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            var client = this.subject.CreateClient();

            // act
            var result = await client.GetAsync(new Uri("router-info", UriKind.Relative), cancellationToken);

            // assert
            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }

        private static void AssertContainsHostAddress(Uri hostAddress, StationEui stationEui, string actual)
        {
            Assert.Contains($"\"uri\":\"{hostAddress}router-data/{stationEui}\"", actual, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> SendSingleMessageAsync(WebSocketClient client, StationEui stationEui, CancellationToken cancellationToken)
        {
            var webSocket = await client.ConnectAsync(new Uri(this.subject.Server.BaseAddress, "router-info"), cancellationToken);
            var result = await SendMessageAsync(webSocket, new { router = stationEui.AsUInt64 }, cancellationToken);
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", cancellationToken);
            return result;
        }

        private static async Task<string> SendMessageAsync(WebSocket webSocket, object payload, CancellationToken cancellationToken)
        {
            await webSocket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var e = webSocket.ReadTextMessages(cancellationToken);
            return !await e.MoveNextAsync() ? throw new InvalidOperationException("No response received.") : e.Current;
        }

        private void SetupIotHubResponse(StationEui stationEui, IList<string> hostAddresses)
        {
            const string networkId = "foo";
            SetupLbsTwinResponse(stationEui, networkId);
            SetupIotHubQueryResponse(networkId, hostAddresses);
        }

        private void SetupLbsTwinResponse(StationEui stationEui, string networkId)
        {
            this.subject
                .RegistryManagerMock?
                .Setup(rm => rm.GetTwinAsync(stationEui.ToString(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Twin { Tags = new TwinCollection(@$"{{""network"":""{networkId}""}}") });
        }

        private void SetupIotHubQueryResponse(string networkId, IList<string> hostAddresses)
        {
            var queryMock = new Mock<IQuery>();
            var i = 0;
            queryMock.Setup(q => q.HasMoreResults).Returns(() => i++ % 2 == 0);
            queryMock.Setup(q => q.GetNextAsJsonAsync()).ReturnsAsync(from ha in hostAddresses
                                                                      select JsonSerializer.Serialize(new { hostAddress = ha }));
            this.subject
                .RegistryManagerMock?
                .Setup(rm => rm.CreateQuery($"SELECT properties.desired.hostAddress FROM devices.modules WHERE tags.network = '{networkId}'"))
                .Returns(queryMock.Object);
        }

        public void Dispose()
        {
            this.subject.Dispose();
        }
    }
}
