// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.IoTHubImpl;
    using LoRaTools.NetworkServerDiscovery;
    using LoRaWan.NetworkServerDiscovery;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
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
        public Mock<IDeviceRegistryManager>? RegistryManagerMock { get; private set; }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                RegistryManagerMock = new Mock<IDeviceRegistryManager>();
                services.RemoveAll<ILnsDiscovery>();
                services.AddSingleton<ILnsDiscovery>(sp => new TagBasedLnsDiscovery(sp.GetRequiredService<IMemoryCache>(), RegistryManagerMock.Object, sp.GetRequiredService<ILogger<TagBasedLnsDiscovery>>()));
            });

            builder.ConfigureLogging(hostBuilder => hostBuilder.ClearProviders());

            return base.CreateHost(builder);
        }
    }

    public sealed class LnsDiscoveryIntegrationTests : IDisposable
    {
        private static readonly string[] HostAddresses = new[] { "ws://foo:5000", "wss://bar:5001" };
        private static readonly StationEui StationEui = new StationEui(1);

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
            var client = this.subject.Server.CreateWebSocketClient();
            SetupIotHubResponse(StationEui, HostAddresses);

            // act
            var result = await SendSingleMessageAsync(client, StationEui, cancellationToken);

            // assert
            AssertContainsHostAddress(new Uri(HostAddresses[0]), StationEui, result);
        }

        [Fact]
        public async Task RouterInfo_Returns_Lns_Using_Round_Robin_On_Rerequest()
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            var client = this.subject.Server.CreateWebSocketClient();
            SetupIotHubResponse(StationEui, HostAddresses);

            // act + assert
            for (var i = 0; i < HostAddresses.Length; ++i)
            {
                var result = await SendSingleMessageAsync(client, StationEui, cancellationToken);
                AssertContainsHostAddress(new Uri(HostAddresses[i % HostAddresses.Length]), StationEui, result);
            }
        }

        public static TheoryData<string> Erroneous_Host_Address_TheoryData() => TheoryDataFactory.From(new[]
        {
            "", "http://mylns:5000", "htt://mylns:5000", "ws:/mylns:5000"
        });

        [Theory]
        [MemberData(nameof(Erroneous_Host_Address_TheoryData))]
        public async Task RouterInfo_Fails_If_All_Lns_Are_Misconfigured(string hostAddress)
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            var client = this.subject.Server.CreateWebSocketClient();
            SetupIotHubResponse(StationEui, new[] { hostAddress });

            // act
            var result = await SendSingleMessageAsync(client, StationEui, cancellationToken);

            // assert
            Assert.Contains("No LNS found in network", result, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [MemberData(nameof(Erroneous_Host_Address_TheoryData))]
        public async Task RouterInfo_Is_Resilient_Against_Misconfigured_Lns(string hostAddress)
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            var client = this.subject.Server.CreateWebSocketClient();
            SetupIotHubResponse(StationEui, HostAddresses.Append(hostAddress).ToList());

            // act + assert
            for (var i = 0; i < HostAddresses.Length; ++i)
            {
                var result = await SendSingleMessageAsync(client, StationEui, cancellationToken);
                AssertContainsHostAddress(new Uri(HostAddresses[i % HostAddresses.Length]), StationEui, result);
            }
        }

        [Fact]
        public async Task RouterInfo_Returns_Same_Lns_For_Different_Stations()
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            var firstStation = new StationEui(1);
            var secondStation = new StationEui(2);
            var client = this.subject.Server.CreateWebSocketClient();
            SetupIotHubResponse(firstStation, HostAddresses);
            SetupIotHubResponse(secondStation, HostAddresses);

            // act
            var firstResult = await SendSingleMessageAsync(client, firstStation, cancellationToken);
            var secondResult = await SendSingleMessageAsync(client, secondStation, cancellationToken);

            // assert
            AssertContainsHostAddress(new Uri(HostAddresses[0]), firstStation, firstResult);
            AssertContainsHostAddress(new Uri(HostAddresses[0]), secondStation, secondResult);
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
            await webSocket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { router = stationEui.AsUInt64 })), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var e = webSocket.ReadTextMessages(cancellationToken);
            var result = !await e.MoveNextAsync() ? throw new InvalidOperationException("No response received.") : e.Current;

            try
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closure", cancellationToken);
            }
            catch (IOException)
            {
                // Remote already closed the connection.
            }

            return result;
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
                .Setup(rm => rm.GetStationTwinAsync(stationEui, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new IoTHubStationTwin(new Twin { Tags = new TwinCollection(@$"{{""network"":""{networkId}""}}") }));
        }

        private void SetupIotHubQueryResponse(string networkId, IList<string> hostAddresses)
        {
            var queryMock = new Mock<IRegistryPageResult<string>>();
            var i = 0;
            queryMock.Setup(q => q.HasMoreResults).Returns(() => i++ % 2 == 0);
            queryMock.Setup(q => q.GetNextPageAsync()).ReturnsAsync(from ha in hostAddresses
                                                                      select JsonSerializer.Serialize(new { hostAddress = ha, deviceId = Guid.NewGuid().ToString() }));
            this.subject
                .RegistryManagerMock?
                .Setup(rm => rm.FindLnsByNetworkId(networkId))
                .Returns(queryMock.Object);
        }

        public void Dispose()
        {
            this.subject.Dispose();
        }
    }
}
