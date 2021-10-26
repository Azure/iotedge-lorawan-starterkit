// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Buffers;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using static Bandwidth;
    using static RouterConfigStationFlags;
    using static SpreadingFactor;

    public class LnsProtocolMessageProcessor : ILnsProtocolMessageProcessor
    {
        private readonly IHttpContextAccessor httpContextAccessor;
        private readonly ILogger<LnsProtocolMessageProcessor> logger;

        public LnsProtocolMessageProcessor(IHttpContextAccessor httpContextAccessor,
                                           ILogger<LnsProtocolMessageProcessor> logger)
        {
            this.httpContextAccessor = httpContextAccessor;
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
        internal async Task<bool> InternalHandleDiscoveryAsync(string json, WebSocket socket, CancellationToken token)
        {
            var stationEui = LnsDiscovery.QueryReader.Read(json);
            this.logger.LogInformation($"Received discovery request from: {stationEui}");

            try
            {
                var httpContext = this.httpContextAccessor.HttpContext;
                var scheme = httpContext.Request.IsHttps ? "wss" : "ws";
                var url = new Uri($"{scheme}://{httpContext.Request.Host}{BasicsStationNetworkServer.DataEndpoint}");

                var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                                                       .SingleOrDefault(ni => ni.GetIPProperties()
                                                                                .UnicastAddresses
                                                                                .Any(info => info.Address.Equals(httpContext.Connection.LocalIpAddress)));

                var muxs = Id6.Format(networkInterface is { } someNetworkInterface
                                      ? someNetworkInterface.GetPhysicalAddress().Convert48To64() : 0,
                                      Id6.FormatOptions.FixedWidth);

                var response = Json.Write(w => LnsDiscovery.WriteResponse(w, stationEui, muxs, url));
                await socket.SendAsync(response, WebSocketMessageType.Text, true, token);
            }
            catch (Exception ex)
            {
                var response = Json.Write(w => LnsDiscovery.WriteResponse(w, stationEui, ex.Message));
                await socket.SendAsync(response, WebSocketMessageType.Text, true, token);
                throw;
            }

            return false;
        }


        /// <returns>A boolean stating if more requests are expected on this endpoint. If false, the underlying socket should be closed.</returns>
        internal async Task<bool> InternalHandleDataAsync(string json, WebSocket socket, CancellationToken token)
        {
            switch (LnsData.MessageTypeReader.Read(json))
            {
                case LnsMessageType.Version:
                    var stationVersion = LnsData.VersionMessageReader.Read(json);
                    this.logger.LogInformation($"Received 'version' message for station '{stationVersion}'.");
                    // A future implementation should retrieve dynamically a SX1301CONF for 'regional' configurations based on the 'stationEui'
                    // Current implementation is statically returning a SX1301CONF for EU863
                    var response = LnsData.WriteRouterConfig(new[] { new NetId(1) },
                                                             new[] { (new JoinEui(ulong.MinValue), new JoinEui(ulong.MaxValue)) },
                                                             "EU863",
                                                             "sx1301/1",
                                                             (new Hertz(863000000), new Hertz(870000000)),
                                                             new[]
                                                             {
                                                                 // The following is actually a tuple of SF, Bandwidth and DownlinkOnly.
                                                                 (SF11, BW125, false),
                                                                 (SF10, BW125, false),
                                                                 (SF9, BW125, false),
                                                                 (SF8, BW125, false),
                                                                 (SF7, BW125, false),
                                                                 (SF7, BW250, false),
                                                             },
                                                             NoClearChannelAssessment | NoDutyCycle | NoDwellTimeLimitations);
                    await socket.SendAsync(Encoding.UTF8.GetBytes(response), WebSocketMessageType.Text, true, token);
                    break;
                case LnsMessageType.JoinRequest:
                    this.logger.LogInformation($"Received 'jreq' message: {json}.");
                    break;
                case LnsMessageType.UplinkDataFrame:
                    this.logger.LogInformation($"Received 'updf' message: {json}.");
                    break;
                case LnsMessageType.TransmitConfirmation:
                    this.logger.LogInformation($"Received 'dntxed' message: {json}.");
                    break;
                case var messageType and (LnsMessageType.DownlinkMessage or LnsMessageType.RouterConfig):
                    throw new NotSupportedException($"'{messageType}' is not a valid message type for this endpoint and is only valid for 'downstream' messages.");
                default:
                    throw new SwitchExpressionException();
            }

            return true;
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
                    }
                }
                catch (OperationCanceledException ex)
#pragma warning disable CA1508 // Avoid dead conditional code (false positive)
                    when (ex is { InnerException: WebSocketException { WebSocketErrorCode: WebSocketError.ConnectionClosedPrematurely } })
#pragma warning restore CA1508 // Avoid dead conditional code
                {
                    // This can happen if the basic station client is losing connectivity
                    this.logger.LogDebug(ex, ex.Message);
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
