// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
namespace LoRaWan.NetworkServer.BasicStation.Controllers
{
    using System;
    using System.Buffers;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicStation.Processors;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [ApiController]
    [Route("/")]
    public class BasicStationController : ControllerBase
    {
        private readonly ILogger<BasicStationController> logger;
        private readonly ILnsProcessor lnsProcessor;

        public BasicStationController(ILogger<BasicStationController> logger, ILnsProcessor lnsProcessor)
        {
            this.logger = logger;
            this.lnsProcessor = lnsProcessor;
        }

        [HttpGet("/router-data")]
        public Task Data(CancellationToken cancellationToken)
            => this.ProcessIncomingRequestAsync(this.lnsProcessor.HandleDataAsync, cancellationToken);

        [HttpGet("/router-info")]
        public Task Discovery(CancellationToken cancellationToken)
            => this.ProcessIncomingRequestAsync(this.lnsProcessor.HandleDiscoveryAsync, cancellationToken);

        internal virtual HttpContext GetHttpContext() => HttpContext;

        internal async Task ProcessIncomingRequestAsync(Func<string, WebSocket, CancellationToken, Task> handler,
                                                       CancellationToken cancellationToken)
        {
            var httpContext = GetHttpContext();
            if (httpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                this.logger.Log(LogLevel.Debug, $"WebSocket connection from {httpContext.Connection.RemoteIpAddress} established");
                ValueWebSocketReceiveResult result;
                var byteArray = ArrayPool<byte>.Shared.Rent(1024);
                try
                {
                    int chunksReceived = 0;
                    int framePayloadSize = 0;
                    Memory<byte> framePayload = null;
                    var buffer = new Memory<byte>(byteArray);
                    do
                    {
                        result = await webSocket.ReceiveAsync(buffer, cancellationToken);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await CloseSocketAsync(webSocket, cancellationToken);
                            return;
                        }
                        else
                        {
                            if (chunksReceived == 0)
                                framePayload = new Memory<byte>(new byte[1024]);

                            var data = buffer.Slice(framePayloadSize, result.Count);
                            data.CopyTo(framePayload.Slice(framePayloadSize, result.Count));
                            framePayloadSize += result.Count;
                            chunksReceived++;
                        }
                    }
                    while (!result.EndOfMessage);

                    var input = Encoding.UTF8.GetString(framePayload.ToArray()).Replace("\0", string.Empty, StringComparison.OrdinalIgnoreCase);

                    await handler(input, webSocket, cancellationToken);

                    await CloseSocketAsync(webSocket, cancellationToken);
                }
                catch (WebSocketException wsException)
                {
                    this.logger.LogError(wsException, wsException.Message);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(byteArray);
                }
            }
            else
            {
                httpContext.Response.StatusCode = 400;
            }
        }

        internal async Task CloseSocketAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            if (socket.State is WebSocketState.Open)
            {
                this.logger.LogDebug($"Closing websocket.");
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, nameof(WebSocketCloseStatus.NormalClosure), cancellationToken);
                this.logger.LogDebug("WebSocket connection closed");
            }
        }
    }
}