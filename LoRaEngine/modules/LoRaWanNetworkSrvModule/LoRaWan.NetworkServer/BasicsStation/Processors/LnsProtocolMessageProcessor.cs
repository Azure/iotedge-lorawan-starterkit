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
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;

    internal class LnsProtocolMessageProcessor : ILnsProtocolMessageProcessor
    {
        private readonly IBasicsStationConfigurationService basicsStationConfigurationService;
        private readonly WebSocketWriterRegistry<StationEui, string> socketWriterRegistry;
        private readonly IPacketForwarder downstreamSender;
        private readonly IMessageDispatcher messageDispatcher;
        private readonly IConcentratorDeduplication concentratorDeduplication;
        private readonly ILogger<LnsProtocolMessageProcessor> logger;

        public LnsProtocolMessageProcessor(IBasicsStationConfigurationService basicsStationConfigurationService,
                                           WebSocketWriterRegistry<StationEui, string> socketWriterRegistry,
                                           IPacketForwarder packetForwarder,
                                           IMessageDispatcher messageDispatcher,
                                           IConcentratorDeduplication concentratorDeduplication,
                                           ILogger<LnsProtocolMessageProcessor> logger)
        {
            this.basicsStationConfigurationService = basicsStationConfigurationService;
            this.socketWriterRegistry = socketWriterRegistry;
            this.downstreamSender = packetForwarder;
            this.messageDispatcher = messageDispatcher;
            this.concentratorDeduplication = concentratorDeduplication;
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

            try
            {
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
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => this.logger.LogError(ex, $"An exception occurred while processing requests: {ex}.")))
            {
                throw;
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

            using var scope = this.logger.BeginEuiScope(stationEui);

            var channel = new WebSocketTextChannel(socket, sendTimeout: TimeSpan.FromSeconds(3));
            var handle = this.socketWriterRegistry.Register(stationEui, channel);

            try
            {
                using var cancellationTokenSource = new CancellationTokenSource();
                using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
                var task = channel.ProcessSendQueueAsync(linkedCancellationTokenSource.Token);

                await using var message = socket.ReadTextMessages(cancellationToken);
                while (await message.MoveNextAsync())
                    await HandleDataMessageAsync(stationEui, handle, message.Current, cancellationToken);

                cancellationTokenSource.Cancel(); // cancel send queue processing, then...

                try
                {
                    await task; // ...wait for its task to complete (practically instantaneous)
                }
                catch (OperationCanceledException)
                {
                    // ignore because it is expected
                }
            }
            finally
            {
                _ = this.socketWriterRegistry.Deregister(stationEui);
            }
        }

        private async Task HandleDataMessageAsync(StationEui stationEui,
                                                  IWebSocketWriterHandle<string> socket,
                                                  string json,
                                                  CancellationToken cancellationToken)
        {
            switch (LnsData.MessageTypeReader.Read(json))
            {
                case LnsMessageType.Version:
                    var stationVersion = LnsData.VersionMessageReader.Read(json);
                    this.logger.LogInformation($"Received 'version' message for station '{stationVersion}'.");
                    var routerConfigResponse = await this.basicsStationConfigurationService.GetRouterConfigMessageAsync(stationEui, cancellationToken);
                    await socket.SendAsync(routerConfigResponse, cancellationToken);
                    break;
                case LnsMessageType.JoinRequest:
                    this.logger.LogInformation($"Received 'jreq' message: {json}.");
                    try
                    {
                        var jreq = LnsData.JoinRequestFrameReader.Read(json);
                        var routerRegion = await this.basicsStationConfigurationService.GetRegionAsync(stationEui, cancellationToken);
                        var rxpk = new BasicStationToRxpk(jreq.RadioMetadata, routerRegion);

                        var loraRequest = new LoRaRequest(rxpk, this.downstreamSender, DateTime.UtcNow);
                        loraRequest.SetPayload(new LoRaPayloadJoinRequestLns(jreq.MacHeader,
                                                                             jreq.JoinEui,
                                                                             jreq.DevEui,
                                                                             jreq.DevNonce,
                                                                             jreq.Mic));
                        loraRequest.SetRegion(routerRegion);
                        loraRequest.SetStationEui(stationEui);
                        this.messageDispatcher.DispatchRequest(loraRequest);

                    }
                    catch (JsonException)
                    {
                        this.logger.LogInformation($"Received unexpected 'jreq' message: {json}.");
                    }
                    break;
                case LnsMessageType.UplinkDataFrame:
                    this.logger.LogInformation($"Received 'updf' message: {json}.");
                    try
                    {
                        var updf = LnsData.UpstreamDataFrameReader.Read(json);

                        if (this.concentratorDeduplication.ShouldDrop(updf, stationEui))
                        {
                            break;
                        }

                        var routerRegion = await this.basicsStationConfigurationService.GetRegionAsync(stationEui, cancellationToken);
                        var rxpk = new BasicStationToRxpk(updf.RadioMetadata, routerRegion);

                        var loraRequest = new LoRaRequest(rxpk, this.downstreamSender, DateTime.UtcNow);
                        loraRequest.SetPayload(new LoRaPayloadDataLns(updf.DevAddr,
                                                                      updf.MacHeader,
                                                                      updf.Control,
                                                                      updf.Counter,
                                                                      updf.Options,
                                                                      updf.Payload,
                                                                      updf.Port,
                                                                      updf.Mic));
                        loraRequest.SetRegion(routerRegion);
                        loraRequest.SetStationEui(stationEui);
                        this.messageDispatcher.DispatchRequest(loraRequest);
                    }
                    catch (JsonException)
                    {
                        this.logger.LogError($"Received unexpected 'updf' message: {json}.");
                    }
                    break;
                case LnsMessageType.TransmitConfirmation:
                    this.logger.LogInformation($"Received 'dntxed' message: {json}.");
                    break;
                case var messageType and (LnsMessageType.DownlinkMessage or LnsMessageType.RouterConfig):
                    throw new NotSupportedException($"'{messageType}' is not a valid message type for this endpoint and is only valid for 'downstream' messages.");
                case var messageType and (LnsMessageType.ProprietaryDataFrame
                                          or LnsMessageType.MulticastSchedule
                                          or LnsMessageType.TimeSync
                                          or LnsMessageType.RunCommand
                                          or LnsMessageType.RemoteShell):
                    this.logger.LogWarning($"'{messageType}' ({messageType.ToBasicStationString()}) is not handled in current LoRaWan Network Server implementation.");
                    break;
                default:
                    throw new SwitchExpressionException();
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
