// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Net.WebSockets;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Jacob;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.NetworkServerDiscovery;
    using LoRaWan.NetworkServer.BasicsStation.JsonHandlers;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Logging;

    internal class LnsProtocolMessageProcessor : ILnsProtocolMessageProcessor
    {
        private static readonly Action<ILogger, string, string, Exception> LogReceivedMessage =
            LoggerMessage.Define<string, string>(LogLevel.Information, default, "Received '{Type}' message: '{Json}'.");

        private readonly IBasicsStationConfigurationService basicsStationConfigurationService;
        private readonly WebSocketWriterRegistry<StationEui, string> socketWriterRegistry;
        private readonly IDownstreamMessageSender downstreamMessageSender;
        private readonly IMessageDispatcher messageDispatcher;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<LnsProtocolMessageProcessor> logger;
        private readonly RegistryMetricTagBag registryMetricTagBag;
        private readonly ITracing tracing;
        private readonly Counter<int> uplinkMessageCounter;
        private readonly Counter<int> unhandledExceptionCount;

        public static readonly DateTime GpsEpoch = new DateTime(1980, 1, 6, 0, 0, 0, DateTimeKind.Utc);

        public LnsProtocolMessageProcessor(IBasicsStationConfigurationService basicsStationConfigurationService,
                                           WebSocketWriterRegistry<StationEui, string> socketWriterRegistry,
                                           IDownstreamMessageSender downstreamMessageSender,
                                           IMessageDispatcher messageDispatcher,
                                           ILoggerFactory loggerFactory,
                                           ILogger<LnsProtocolMessageProcessor> logger,
                                           RegistryMetricTagBag registryMetricTagBag,
                                           Meter meter,
                                           ITracing tracing)
        {
            this.basicsStationConfigurationService = basicsStationConfigurationService;
            this.socketWriterRegistry = socketWriterRegistry;
            this.downstreamMessageSender = downstreamMessageSender;
            this.messageDispatcher = messageDispatcher;
            this.loggerFactory = loggerFactory;
            this.logger = logger;
            this.registryMetricTagBag = registryMetricTagBag;
            this.tracing = tracing;
            this.uplinkMessageCounter = meter?.CreateCounter<int>(MetricRegistry.D2CMessagesReceived);
            this.unhandledExceptionCount = meter?.CreateCounter<int>(MetricRegistry.UnhandledExceptions);
        }

        public Task HandleDiscoveryAsync(HttpContext httpContext, CancellationToken cancellationToken) =>
            ExecuteWithExceptionHandlingAsync(async () =>
            {
                var uriBuilder = new UriBuilder
                {
                    Scheme = httpContext.Request.IsHttps ? "wss" : "ws",
                    Host = httpContext.Request.Host.Host
                };

                if (httpContext.Request.Host.Port is { } somePort)
                    uriBuilder.Port = somePort;

                var discoveryService = new DiscoveryService(new LocalLnsDiscovery(uriBuilder.Uri), this.loggerFactory.CreateLogger<DiscoveryService>());
                await discoveryService.HandleDiscoveryRequestAsync(httpContext, cancellationToken);
                return 0;
            });

        public Task HandleDataAsync(HttpContext httpContext, CancellationToken cancellationToken) =>
            ExecuteWithExceptionHandlingAsync(async () =>
            {
                var webSocketConnection = new WebSocketConnection(httpContext, this.logger);
                return await webSocketConnection.HandleAsync((httpContext, socket, ct) => InternalHandleDataAsync(httpContext.Request.RouteValues, socket, ct), cancellationToken);
            });

        internal async Task InternalHandleDataAsync(RouteValueDictionary routeValues, WebSocket socket, CancellationToken cancellationToken)
        {
            var stationEui = routeValues.TryGetValue(BasicsStationNetworkServer.RouterIdPathParameterName, out var sEui) ?
                StationEui.Parse(sEui.ToString())
                : throw new InvalidOperationException($"{BasicsStationNetworkServer.RouterIdPathParameterName} was not present on path.");

            using var scope = this.logger.BeginEuiScope(stationEui);
            this.registryMetricTagBag.StationEui.Value = stationEui;

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
            using var dataOperation = this.tracing.TrackDataMessage();

            switch (LnsData.MessageTypeReader.Read(json))
            {
                case LnsMessageType.Version:
                    var (version, package) = LnsData.VersionMessageReader.Read(json);
                    this.logger.LogInformation("Received 'version' message for station '{StationVersion}' with package '{StationPackage}'.", version, package);
                    await this.basicsStationConfigurationService.SetReportedPackageVersionAsync(stationEui, package, cancellationToken);
                    var routerConfigResponse = await this.basicsStationConfigurationService.GetRouterConfigMessageAsync(stationEui, cancellationToken);
                    await socket.SendAsync(routerConfigResponse, cancellationToken);
                    break;
                case LnsMessageType.JoinRequest:
                    LogReceivedMessage(this.logger, "jreq", json, null);
                    try
                    {
                        var jreq = LnsData.JoinRequestFrameReader.Read(json);

                        var routerRegion = await this.basicsStationConfigurationService.GetRegionAsync(stationEui, cancellationToken);

                        var loraRequest = new LoRaRequest(jreq.RadioMetadata, this.downstreamMessageSender, DateTime.UtcNow);
                        loraRequest.SetPayload(new LoRaPayloadJoinRequest(jreq.JoinEui,
                                                                          jreq.DevEui,
                                                                          jreq.DevNonce,
                                                                          jreq.Mic));
                        loraRequest.SetRegion(routerRegion);
                        loraRequest.SetStationEui(stationEui);
                        this.messageDispatcher.DispatchRequest(loraRequest);

                    }
                    catch (JsonException)
                    {
                        this.logger.LogInformation("Received unexpected 'jreq' message: {Json}.", json);
                    }
                    break;
                case LnsMessageType.UplinkDataFrame:
                    LogReceivedMessage(this.logger, "updf", json, null);
                    try
                    {
                        var updf = LnsData.UpstreamDataFrameReader.Read(json);

                        using var scope = this.logger.BeginDeviceAddressScope(updf.DevAddr);
                        this.uplinkMessageCounter?.Add(1);

                        var routerRegion = await this.basicsStationConfigurationService.GetRegionAsync(stationEui, cancellationToken);

                        var loraRequest = new LoRaRequest(updf.RadioMetadata, this.downstreamMessageSender, DateTime.UtcNow);
                        loraRequest.SetPayload(new LoRaPayloadData(updf.DevAddr,
                                                                   updf.MacHeader,
                                                                   updf.FrameControlFlags,
                                                                   updf.Counter,
                                                                   updf.Options,
                                                                   updf.Payload,
                                                                   updf.Port,
                                                                   updf.Mic,
                                                                   this.logger));
                        loraRequest.SetRegion(routerRegion);
                        loraRequest.SetStationEui(stationEui);
                        this.messageDispatcher.DispatchRequest(loraRequest);
                    }
                    catch (JsonException)
                    {
                        this.logger.LogError("Received unexpected 'updf' message: {Json}.", json);
                    }
                    break;
                case LnsMessageType.TransmitConfirmation:
                    LogReceivedMessage(this.logger, "dntxed", json, null);
                    break;
                case var messageType and (LnsMessageType.DownlinkMessage or LnsMessageType.RouterConfig):
                    throw new NotSupportedException($"'{messageType}' is not a valid message type for this endpoint and is only valid for 'downstream' messages.");
                case LnsMessageType.TimeSync:
                    var timeSyncData = JsonSerializer.Deserialize<TimeSyncMessage>(json);
                    LogReceivedMessage(this.logger, "TimeSync", json, null);
                    timeSyncData.GpsTime = (ulong)DateTime.UtcNow.Subtract(GpsEpoch).TotalMilliseconds * 1000; // to microseconds
                    await socket.SendAsync(JsonSerializer.Serialize(timeSyncData), cancellationToken);
                    break;
                case var messageType and (LnsMessageType.ProprietaryDataFrame
                                          or LnsMessageType.MulticastSchedule
                                          or LnsMessageType.RunCommand
                                          or LnsMessageType.RemoteShell):
                    this.logger.LogWarning("'{MessageType}' ({MessageTypeBasicStationString}) is not handled in current LoRaWan Network Server implementation.", messageType, messageType.ToBasicStationString());
                    break;
                default:
                    throw new SwitchExpressionException();
            }
        }

        private async Task<T> ExecuteWithExceptionHandlingAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => this.logger.LogError(ex, "An exception occurred while processing requests: {Exception}.", ex),
                                                                    () => this.unhandledExceptionCount?.Add(1)))
            {
                throw;
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
