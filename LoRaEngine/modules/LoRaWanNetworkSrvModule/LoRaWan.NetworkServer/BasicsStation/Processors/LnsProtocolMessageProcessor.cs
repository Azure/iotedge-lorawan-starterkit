// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    public class LnsProtocolMessageProcessor : ILnsProtocolMessageProcessor
    {
        private readonly ILogger<LnsProtocolMessageProcessor> logger;

        public LnsProtocolMessageProcessor(ILogger<LnsProtocolMessageProcessor> logger)
        {
            this.logger = logger;
        }

        public async Task HandleDiscoveryAsync(HttpContext httpContext, CancellationToken token)
        {
            if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));

            _ = await ProcessIncomingRequestAsync(httpContext, InternalHandleDiscoveryAsync, token);
        }

        public async Task HandleDataAsync(HttpContext httpContext, CancellationToken token)
        {
            if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));

            _ = await ProcessIncomingRequestAsync(httpContext, InternalHandleDataAsync, token);
        }

        /// <returns>A boolean stating if more requests are expected on this endpoint. If false, the underlying socket should be closed.</returns>
        internal Task<bool> InternalHandleDiscoveryAsync(string json, WebSocket socket, CancellationToken token)
        {
            this.logger.LogInformation($"Received message: {json}");
            return Task.FromResult(false);
        }

        /// <returns>A boolean stating if more requests are expected on this endpoint. If false, the underlying socket should be closed.</returns>
        internal Task<bool> InternalHandleDataAsync(string json, WebSocket socket, CancellationToken token)
        {
            this.logger.LogInformation($"Received message: {json}");
            return Task.FromResult(false);
        }

        internal async Task<HttpContext> ProcessIncomingRequestAsync(HttpContext httpContext,
                                                                   Func<string, WebSocket, CancellationToken, Task<bool>> handler,
                                                                   CancellationToken cancellationToken)
        {
            if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));
            if (handler is null) throw new ArgumentNullException(nameof(handler));

            if (httpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await httpContext.WebSockets.AcceptWebSocketAsync();
                this.logger.Log(LogLevel.Debug, $"WebSocket connection from {httpContext.Connection.RemoteIpAddress} established");
                try
                {
                    while (true)
                    {
                        ValueWebSocketReceiveResult result;
                        using var buffer = MemoryPool<byte>.Shared.Rent(1024);
                        using var ms = new MemoryStream(buffer.Memory.Length);
                        do
                        {
                            result = await webSocket.ReceiveAsync(buffer.Memory, cancellationToken);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await CloseSocketAsync(webSocket, cancellationToken);
                                return httpContext;
                            }
                            else
                            {
                                ms.Write(buffer.Memory.Span[..result.Count]);
                            }
                        }
                        while (!result.EndOfMessage);

                        ms.Position = 0;

                        string input;
                        using (var reader = new StreamReader(ms))
                            input = reader.ReadToEnd();

                        if (!await handler(input, webSocket, cancellationToken))
                        {
                            await CloseSocketAsync(webSocket, cancellationToken);
                            break;
                        }
                    };
                }
                catch (WebSocketException wsException)
                {
                    this.logger.LogError(wsException, wsException.Message);
                }
            }
            else
            {
                httpContext.Response.StatusCode = 400;
            }
            return httpContext;
        }

        internal async Task CloseSocketAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            if (socket.State is WebSocketState.Open)
            {
                this.logger.LogDebug("Closing websocket.");
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, nameof(WebSocketCloseStatus.NormalClosure), cancellationToken);
                this.logger.LogDebug("WebSocket connection closed");
            }
        }
    }
}
