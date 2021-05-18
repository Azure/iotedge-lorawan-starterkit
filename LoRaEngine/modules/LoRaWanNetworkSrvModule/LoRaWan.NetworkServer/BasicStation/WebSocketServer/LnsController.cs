// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.WebSocketServer
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicStation.Models;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging;

    [ApiController]
    [Route("/")]
    public class LnsController : ControllerBase
    {
        private readonly ILogger<LnsController> logger;

        public LnsController(ILogger<LnsController> logger)
        {
            this.logger = logger;
        }

        [HttpGet("/router-config")]
        public Task RouterConfig(CancellationToken cancellationToken)
            => this.HandleRequestAsync(this.AnswerRouterConfig, cancellationToken);

        [HttpGet("/router-info")]
        public Task RouterInfo(CancellationToken cancellationToken)
            => this.HandleRequestAsync(this.AnswerRouterInfo, cancellationToken);

        private async Task HandleRequestAsync(Func<WebSocket, CancellationToken, Task> handler, CancellationToken cancellationToken)
        {
            if (this.HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await this.HttpContext.WebSockets.AcceptWebSocketAsync();
                this.logger.Log(LogLevel.Debug, "WebSocket connection established");
                await handler(webSocket, cancellationToken);
            }
            else
            {
                this.HttpContext.Response.StatusCode = 400;
            }
        }

        private async Task AnswerRouterInfo(WebSocket socket, CancellationToken cancellationToken)
        {
            ValueWebSocketReceiveResult result;
            var sharedArrayPool = ArrayPool<byte>.Shared;
            var byteArray = sharedArrayPool.Rent(1024);
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    int chunksRecieved = 0;
                    int framePayloadSize = 0;
                    Memory<byte> framePayload = null;
                    var buffer = new Memory<byte>(byteArray);
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, cancellationToken);

                        if (chunksRecieved == 0)
                            framePayload = new Memory<byte>(new byte[result.Count]);

                        var data = buffer.Slice(0, result.Count);
                        data.CopyTo(framePayload.Slice(framePayloadSize, result.Count));
                        framePayloadSize += result.Count;
                        chunksRecieved++;
                    }
                    while (!result.EndOfMessage);

                    var input = Encoding.UTF8.GetString(framePayload.ToArray()).Replace("\0", string.Empty);
                    this.logger.Log(LogLevel.Information, $"Received message: {input}");

                    var reply = JsonSerializer.Deserialize<LnsDiscoveryReply>(input);
                    reply.Router = "1";
                    reply.Uri = $"ws://{this.Request.Host.Value}/router-config";
                    reply.Muxs = "00-00-00-00-00-00-00-00";
                    reply.Error = null;
                    this.logger.Log(LogLevel.Debug, JsonSerializer.Serialize(reply));

                    var options = new JsonSerializerOptions
                    {
                        IgnoreNullValues = true
                    };

                    var message = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reply, options));
                    await socket.SendAsync(new ReadOnlyMemory<byte>(message, 0, message.Length), result.MessageType, result.EndOfMessage, cancellationToken);
                    this.logger.Log(LogLevel.Information, "Message sent to Client");

                    result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                    this.logger.Log(LogLevel.Information, "Message received from Client");
                    buffer = null;
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, nameof(WebSocketCloseStatus.NormalClosure), cancellationToken);
                    this.logger.Log(LogLevel.Information, "WebSocket connection closed");
                }
            }
            finally
            {
                sharedArrayPool.Return(byteArray);
            }
        }

        private async Task AnswerRouterConfig(WebSocket socket, CancellationToken cancellationToken)
        {
            ValueWebSocketReceiveResult result;
            var sharedArrayPool = ArrayPool<byte>.Shared;
            var byteArray = sharedArrayPool.Rent(1024);
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    int chunksRecieved = 0;
                    int framePayloadSize = 0;
                    Memory<byte> framePayload = null;
                    var buffer = new Memory<byte>(byteArray);
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, cancellationToken);

                        if (chunksRecieved == 0)
                            framePayload = new Memory<byte>(new byte[result.Count]);

                        var data = buffer.Slice(0, result.Count);
                        data.CopyTo(framePayload.Slice(framePayloadSize, result.Count));
                        framePayloadSize += result.Count;
                        chunksRecieved++;
                    }
                    while (!result.EndOfMessage);

                    var input = Encoding.UTF8.GetString(framePayload.ToArray()).Replace("\0", string.Empty);
                    this.logger.Log(LogLevel.Information, "Message received from Client");
                    var formaterInput = JsonSerializer.Deserialize<LnsDiscoveryVersion>(input);
                    if (formaterInput?.Msgtype == "version")
                    {
                        var message = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new LnsRouterConfigReply { Sx1301_conf = new List<Sx1301Config>() { new Sx1301Config() } }));
                        await socket.SendAsync(new ArraySegment<byte>(message, 0, message.Length), result.MessageType, result.EndOfMessage, CancellationToken.None);
                        this.logger.Log(LogLevel.Information, "Message sent to Client");
                    }

                    result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                }
            }
            finally
            {
                sharedArrayPool.Return(byteArray);
            }
        }
    }
}
