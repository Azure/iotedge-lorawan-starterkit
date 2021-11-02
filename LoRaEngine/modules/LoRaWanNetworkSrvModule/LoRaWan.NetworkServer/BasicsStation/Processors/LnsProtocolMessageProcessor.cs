// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;

    internal class LnsProtocolMessageProcessor : ILnsProtocolMessageProcessor
    {
        private readonly IBasicsStationConfigurationService basicsStationConfigurationService;
        private readonly WebSocketWriterRegistry<StationEui, string> socketWriterRegistry;
        private readonly ILogger<LnsProtocolMessageProcessor> logger;

        public LnsProtocolMessageProcessor(IBasicsStationConfigurationService basicsStationConfigurationService,
                                           WebSocketWriterRegistry<StationEui, string> socketWriterRegistry,
                                           ILogger<LnsProtocolMessageProcessor> logger)
        {
            this.basicsStationConfigurationService = basicsStationConfigurationService;
            this.socketWriterRegistry = socketWriterRegistry;
            this.logger = logger;
        }

        internal async Task<HttpContext> ProcessIncomingRequestAsync(HttpContext httpContext,
                                                                     Func<HttpContext, WebSocket, CancellationToken, Task> handler,
                                                                     CancellationToken cancellationToken)
        {
            if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));

            if (!httpContext.WebSockets.IsWebSocketRequest)
            {
                httpContext.Response.StatusCode = 400;
                return httpContext;
            }

            using var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
            this.logger.Log(LogLevel.Debug, $"WebSocket connection from {httpContext.Connection.RemoteIpAddress} established");

            try
            {
                await handler(httpContext, socket, cancellationToken);
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", cancellationToken);
            }
            catch (OperationCanceledException ex)
#pragma warning disable CA1508 // Avoid dead conditional code (false positive)
                when (ex is { InnerException: WebSocketException { WebSocketErrorCode: WebSocketError.ConnectionClosedPrematurely } })
#pragma warning restore CA1508 // Avoid dead conditional code
            {
                // This can happen if the basic station client is losing connectivity
                this.logger.LogDebug(ex, ex.Message);
            }

            return httpContext;
        }

        public Task HandleDiscoveryAsync(HttpContext httpContext, CancellationToken cancellationToken) =>
            ProcessIncomingRequestAsync(httpContext, InternalHandleDiscoveryAsync, cancellationToken);

        public Task HandleDataAsync(HttpContext httpContext, CancellationToken cancellationToken) =>
            ProcessIncomingRequestAsync(httpContext,
                                        (httpContext, socket, ct) => InternalHandleDataAsync(httpContext.Request.RouteValues, socket, ct),
                                        cancellationToken);

        /// <returns>A boolean stating if more requests are expected on this endpoint. If false, the underlying socket should be closed.</returns>
        internal async Task InternalHandleDiscoveryAsync(HttpContext httpContext, WebSocket socket, CancellationToken cancellationToken)
        {
            await using var message = socket.ReadTextMessages(cancellationToken);
            if (!await message.MoveNextAsync())
            {
                this.logger.LogWarning($"Did not receive discovery request from station.");
            }
            else
            {
                var json = message.Current;
                var stationEui = LnsDiscovery.QueryReader.Read(json);
                this.logger.LogInformation($"Received discovery request from: {stationEui}");

                try
                {
                    var scheme = httpContext.Request.IsHttps ? "wss" : "ws";
                    var url = new Uri($"{scheme}://{httpContext.Request.Host}{BasicsStationNetworkServer.DataEndpoint}/{stationEui}");

                    var networkInterface = NetworkInterface.GetAllNetworkInterfaces()
                                                           .SingleOrDefault(ni => ni.GetIPProperties()
                                                                                .UnicastAddresses
                                                                                .Any(info => info.Address.Equals(httpContext.Connection.LocalIpAddress)));

                    var muxs = Id6.Format(networkInterface is { } someNetworkInterface
                                              ? someNetworkInterface.GetPhysicalAddress().Convert48To64() : 0,
                                          Id6.FormatOptions.FixedWidth);

                    var response = Json.Write(w => LnsDiscovery.WriteResponse(w, stationEui, muxs, url));
                    await socket.SendAsync(response, WebSocketMessageType.Text, true, cancellationToken);
                }
                catch (Exception ex)
                {
                    var response = Json.Write(w => LnsDiscovery.WriteResponse(w, stationEui, ex.Message));
                    await socket.SendAsync(response, WebSocketMessageType.Text, true, cancellationToken);
                    throw;
                }
            }
        }

        internal async Task InternalHandleDataAsync(RouteValueDictionary routeValues, WebSocket socket, CancellationToken cancellationToken)
        {
            var stationEui = routeValues.TryGetValue(BasicsStationNetworkServer.RouterIdPathParameterName, out var sEui) ?
                StationEui.Parse(sEui.ToString())
                : throw new InvalidOperationException($"{BasicsStationNetworkServer.RouterIdPathParameterName} was not present on path.");

            var channel = new WebSocketTextChannel(socket, sendTimeout: TimeSpan.FromSeconds(3));
            _ = socketWriterRegistry.Register(stationEui, channel);

            using var cancellationTokenSource = new CancellationTokenSource();
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
            var task = channel.ProcessSendQueueAsync(linkedCancellationTokenSource.Token);

            await using var message = socket.ReadTextMessages(cancellationToken);
            while (await message.MoveNextAsync())
            {
                var json = message.Current;
                switch (LnsData.MessageTypeReader.Read(json))
                {
                    case LnsMessageType.Version:
                        var stationVersion = LnsData.VersionMessageReader.Read(json);
                        this.logger.LogInformation($"Received 'version' message for station '{stationVersion}'.");
                        var response = await basicsStationConfigurationService.GetRouterConfigMessageAsync(stationEui, cancellationToken);
                        await socket.SendAsync(Encoding.UTF8.GetBytes(response), WebSocketMessageType.Text, true, cancellationToken);
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
            }

            _ = socketWriterRegistry.Deregister(stationEui);
            cancellationTokenSource.Cancel();

            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // ignore because it is expected
            }
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
