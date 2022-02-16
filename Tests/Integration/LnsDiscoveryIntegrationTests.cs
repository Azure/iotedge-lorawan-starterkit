// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Net;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Xunit;

    internal sealed class LnsDiscoveryApplication : WebApplicationFactory<Program>
    { }

    public sealed class LnsDiscoveryIntegrationTests
    {
        [Fact]
        public async Task RouterInfo_Returns_Dummy_Lns()
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            using var subject = new LnsDiscoveryApplication();
            var client = subject.Server.CreateWebSocketClient();
            var webSocket = await client.ConnectAsync(new Uri(subject.Server.BaseAddress, "router-info"), cancellationToken);

            // act
            var result = await SendMessageAsync(webSocket, new { router = 1 }, cancellationToken);

            // assert
            Assert.Contains("aka.ms", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RouterInfo_Returns_400_If_Connection_Is_Not_WebSocket()
        {
            // arrange
            var cancellationToken = CancellationToken.None;
            using var subject = new LnsDiscoveryApplication();
            var client = subject.CreateClient();

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
    }
}
