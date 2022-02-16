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
            SetupIotHubResponse(hostAddresses);

            // act
            var result = await SendMessageAsync(webSocket, new { router = stationEui.AsUInt64 }, cancellationToken);

            // assert
            Assert.Contains($"\"uri\":\"{hostAddresses.First()}/router-data/{stationEui}\"", result, StringComparison.OrdinalIgnoreCase);
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

        private static async Task<string> SendMessageAsync(WebSocket webSocket, object payload, CancellationToken cancellationToken)
        {
            await webSocket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            var e = webSocket.ReadTextMessages(cancellationToken);
            return !await e.MoveNextAsync() ? throw new InvalidOperationException("No response received.") : e.Current;
        }

        private void SetupIotHubResponse(IList<string> hostAddresses)
        {
            this.subject
                .RegistryManagerMock?
                .Setup(rm => rm.GetTwinAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Twin { Tags = new TwinCollection(@"{""network"":""foo""}") });

            var queryMock = new Mock<IQuery>();
            queryMock.SetupSequence(q => q.HasMoreResults).Returns(true).Returns(false);
            queryMock.Setup(q => q.GetNextAsJsonAsync()).ReturnsAsync(from ha in hostAddresses
                                                                      select JsonSerializer.Serialize(new { hostAddress = ha }));
            this.subject
                .RegistryManagerMock?
                .Setup(rm => rm.CreateQuery(It.IsAny<string>()))
                .Returns(queryMock.Object);
        }

        public void Dispose()
        {
            this.subject.Dispose();
        }
    }
}
