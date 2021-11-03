// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.ADR;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines udp Server communicating with packet forwarder.
    /// </summary>
    public sealed class UdpServer : IPacketForwarder, IDisposable
    {
        private const int PORT = 1680;
        private readonly NetworkServerConfiguration configuration;
        private readonly MessageDispatcher messageDispatcher;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        private readonly RandomNumberGenerator RndKeysGenerator = new RNGCryptoServiceProvider();

        private IClassCDeviceMessageSender classCMessageSender;
        private ModuleClient ioTHubModuleClient;
        private volatile int pullAckRemoteLoRaAggregatorPort;
        private volatile string pullAckRemoteLoRaAddress;
        private UdpClient udpClient;

        private Task<byte[]> GetTokenAsync()
        {
            var token = new byte[2];
            this.RndKeysGenerator.GetBytes(token);
            return Task.FromResult(token);
        }

        // Creates a new instance of UdpServer
        public static UdpServer Create()
        {
            var configuration = NetworkServerConfiguration.CreateFromEnvironmentVariables();

            var loRaDeviceAPIService = new LoRaDeviceAPIService(configuration, new ServiceFacadeHttpClientProvider(configuration, ApiVersion.LatestVersion));
            var frameCounterStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(configuration, loRaDeviceAPIService);
            var deduplicationStrategyFactory = new DeduplicationStrategyFactory(loRaDeviceAPIService);
            var adrStrategyProvider = new LoRaADRStrategyProvider();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var dataHandlerImplementation = new DefaultLoRaDataRequestHandler(configuration, frameCounterStrategyProvider, new LoRaPayloadDecoder(), deduplicationStrategyFactory, adrStrategyProvider, new LoRAADRManagerFactory(loRaDeviceAPIService), new FunctionBundlerProvider(loRaDeviceAPIService));
            var connectionManager = new LoRaDeviceClientConnectionManager(cache);
            var loRaDeviceFactory = new LoRaDeviceFactory(configuration, dataHandlerImplementation, connectionManager);
            var loRaDeviceRegistry = new LoRaDeviceRegistry(configuration, cache, loRaDeviceAPIService, loRaDeviceFactory);

            var messageDispatcher = new MessageDispatcher(configuration, loRaDeviceRegistry, frameCounterStrategyProvider);
            var udpServer = new UdpServer(configuration, messageDispatcher, loRaDeviceAPIService, loRaDeviceRegistry);

            // TODO: review dependencies
            var classCMessageSender = new DefaultClassCDevicesMessageSender(configuration, loRaDeviceRegistry, udpServer, frameCounterStrategyProvider);
            dataHandlerImplementation.SetClassCMessageSender(classCMessageSender);

            udpServer.SetClassCMessageSender(classCMessageSender);
            return udpServer;
        }

        // Creates a new instance of UdpServer
        public UdpServer(
            NetworkServerConfiguration configuration,
            MessageDispatcher messageDispatcher,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceRegistry loRaDeviceRegistry)
        {
            this.configuration = configuration;
            this.messageDispatcher = messageDispatcher;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
        }

        public static async Task RunServerAsync()
        {
            Logger.LogAlways("Starting LoRaWAN Server...");

            try
            {
                using var udpServer = UdpServer.Create();

                await udpServer.InitCallBack();

                await udpServer.RunUdpListener();
            }
            catch (Exception ex)
            {
                Logger.Log($"Initialization failed with error: {ex.Message}", LogLevel.Error);
                throw;
            }
        }

        private async Task UdpSendMessageAsync(byte[] messageToSend, string remoteLoRaAggregatorIp, int remoteLoRaAggregatorPort)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                var bytesSent = await this.udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp, remoteLoRaAggregatorPort);
                if (bytesSent < messageToSend.Length)
                {
                    Logger.Log($"Incomplete message transfer from {nameof(UdpServer)}", LogLevel.Warning);
                }
            }
        }

        private async Task RunUdpListener()
        {
            var endPoint = new IPEndPoint(IPAddress.Any, PORT);
            this.udpClient = new UdpClient(endPoint);

            Logger.LogAlways($"LoRaWAN server started on port {PORT}");

            while (true)
            {
                var receivedResults = await this.udpClient.ReceiveAsync();
                var startTimeProcessing = DateTime.UtcNow;

                switch (PhysicalPayload.GetIdentifierFromPayload(receivedResults.Buffer))
                {
                    // In this case we have a keep-alive PULL_DATA packet we don't need to start the engine and can return immediately a response to the challenge
                    case PhysicalIdentifier.PullData:
                        if (this.pullAckRemoteLoRaAggregatorPort == 0)
                        {
                            this.pullAckRemoteLoRaAggregatorPort = receivedResults.RemoteEndPoint.Port;
                            this.pullAckRemoteLoRaAddress = receivedResults.RemoteEndPoint.Address.ToString();
                        }

                        SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PullAck, receivedResults.RemoteEndPoint);
                        break;

                    // This is a PUSH_DATA (upstream message).
                    case PhysicalIdentifier.PushData:
                        SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PushAck, receivedResults.RemoteEndPoint);
                        DispatchMessages(receivedResults.Buffer, startTimeProcessing);

                        break;

                    // This is a ack to a transmission we did previously
                    case PhysicalIdentifier.TxAck:
                        if (receivedResults.Buffer.Length == 12)
                        {
                            Logger.Log(
                                "UDP",
                                $"packet with id {ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2))} successfully transmitted by the aggregator",
                                LogLevel.Debug);
                        }
                        else
                        {
                            var logMsg = string.Format(
                                    CultureInfo.InvariantCulture,
                                    "packet with id {0} had a problem to be transmitted over the air :{1}",
                                    receivedResults.Buffer.Length > 2 ? ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2)) : string.Empty,
                                    receivedResults.Buffer.Length > 12 ? Encoding.UTF8.GetString(receivedResults.Buffer.RangeSubset(12, receivedResults.Buffer.Length - 12)) : string.Empty);
                            Logger.Log("UDP", logMsg, LogLevel.Error);
                        }

                        break;
                    case PhysicalIdentifier.PushAck:
                    case PhysicalIdentifier.PullResp:
                    case PhysicalIdentifier.PullAck:
                    case PhysicalIdentifier.Unknown:
                    default:
                        Logger.Log("UDP", "unknown packet type or length being received", LogLevel.Error);
                        break;
                }
            }
        }

        private void DispatchMessages(byte[] buffer, DateTime startTimeProcessing)
        {
            try
            {
                var messageRxpks = Rxpk.CreateRxpk(buffer);
                if (messageRxpks != null)
                {
                    foreach (var rxpk in messageRxpks)
                    {
                        this.messageDispatcher.DispatchRequest(new LoRaRequest(rxpk, this, startTimeProcessing));
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types.
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Logger.Log("UDP", $"failed to dispatch messages: {ex.Message}", LogLevel.Error);
            }
        }

        private void SendAcknowledgementMessage(UdpReceiveResult receivedResults, byte messageType, IPEndPoint remoteEndpoint)
        {
            var response = new byte[4]
            {
                receivedResults.Buffer[0],
                receivedResults.Buffer[1],
                receivedResults.Buffer[2],
                messageType
            };
            _ = UdpSendMessageAsync(response, remoteEndpoint.Address.ToString(), remoteEndpoint.Port);
        }

        private async Task InitCallBack()
        {
            ITransportSettings transportSettings = new AmqpTransportSettings(Microsoft.Azure.Devices.Client.TransportType.Amqp_Tcp_Only);

            ITransportSettings[] settings = { transportSettings };

            // if running as Edge module
            if (this.configuration.RunningAsIoTEdgeModule)
            {
                this.ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

                Logger.Init(new LoggerConfiguration
                {
                    ModuleClient = this.ioTHubModuleClient,
                    LogLevel = LoggerConfiguration.InitLogLevel(this.configuration.LogLevel),
                    LogToConsole = this.configuration.LogToConsole,
                    LogToHub = this.configuration.LogToHub,
                    LogToUdp = this.configuration.LogToUdp,
                    LogToUdpPort = this.configuration.LogToUdpPort,
                    LogToUdpAddress = this.configuration.LogToUdpAddress,
                    GatewayId = this.configuration.GatewayID
                });

                if (this.configuration.IoTEdgeTimeout > 0)
                {
                    this.ioTHubModuleClient.OperationTimeoutInMilliseconds = this.configuration.IoTEdgeTimeout;
                    Logger.Log($"Changing timeout to {this.ioTHubModuleClient.OperationTimeoutInMilliseconds} ms", LogLevel.Debug);
                }

                Logger.Log("Getting properties from module twin...", LogLevel.Information);

                var moduleTwin = await this.ioTHubModuleClient.GetTwinAsync();
                var moduleTwinCollection = moduleTwin.Properties.Desired;
                try
                {
                    this.loRaDeviceAPIService.URL = new Uri(moduleTwinCollection["FacadeServerUrl"].Value);
                    Logger.LogAlways($"Facade function url: {this.loRaDeviceAPIService.URL}");
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new LoRaProcessingException("Module twin 'FacadeServerUrl' property does not exist.", LoRaProcessingErrorCode.InvalidModuleConfiguration);
                }

                try
                {
                    this.loRaDeviceAPIService.SetAuthCode(moduleTwinCollection["FacadeAuthCode"].Value as string);
                }
                catch (ArgumentOutOfRangeException)
                {
                    throw new LoRaProcessingException("Module twin 'FacadeAuthCode' does not exist.", LoRaProcessingErrorCode.InvalidModuleConfiguration);
                }

                await this.ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);

                await this.ioTHubModuleClient.SetMethodDefaultHandlerAsync(OnDirectMethodCalled, null);
            }

            // running as non edge module for test and debugging
            else
            {
                Logger.Init(new LoggerConfiguration
                {
                    ModuleClient = null,
                    LogLevel = LoggerConfiguration.InitLogLevel(this.configuration.LogLevel),
                    LogToConsole = this.configuration.LogToConsole,
                    LogToHub = this.configuration.LogToHub,
                    LogToUdp = this.configuration.LogToUdp,
                    LogToUdpPort = this.configuration.LogToUdpPort,
                    LogToUdpAddress = this.configuration.LogToUdpAddress,
                    GatewayId = this.configuration.GatewayID
                });
            }

            // Report Log level
            Logger.LogAlways($"Log Level: {Logger.LoggerLevel}");
        }

        private async Task<MethodResponse> OnDirectMethodCalled(MethodRequest methodRequest, object userContext)
        {
            string devEui = null;

            try
            {
                if (string.Equals("clearcache", methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return await ClearCache();
                }
                else if (string.Equals(Constants.CloudToDeviceDecoderElementName, methodRequest.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (this.classCMessageSender == null)
                    {
                        return new MethodResponse((int)HttpStatusCode.NotFound);
                    }

                    var c2d = JsonConvert.DeserializeObject<ReceivedLoRaCloudToDeviceMessage>(methodRequest.DataAsJson);
                    devEui = c2d.DevEUI;
                    Logger.Log(devEui, $"received cloud to device message from direct method: {methodRequest.DataAsJson}", LogLevel.Debug);

                    using var cts = methodRequest.ResponseTimeout.HasValue ? new CancellationTokenSource(methodRequest.ResponseTimeout.Value) : null;

                    if (await this.classCMessageSender.SendAsync(c2d, cts?.Token ?? CancellationToken.None))
                    {
                        return new MethodResponse((int)HttpStatusCode.OK);
                    }

                    return new MethodResponse((int)HttpStatusCode.BadRequest);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(devEui, $"[class-c] error sending class C cloud to device message. {ex.Message}", LogLevel.Error);
                throw;
            }

            Logger.Log($"Unknown direct method called: {methodRequest?.Name}", LogLevel.Error);

            return new MethodResponse(404);
        }

        private Task<MethodResponse> ClearCache()
        {
            this.loRaDeviceRegistry.ResetDeviceCache();

            return Task.FromResult(new MethodResponse(200));
        }

        private Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                if (desiredProperties.Contains("FacadeServerUrl"))
                {
                    this.loRaDeviceAPIService.URL = new Uri(desiredProperties["FacadeServerUrl"]);
                }

                if (desiredProperties.Contains("FacadeAuthCode"))
                {
                    this.loRaDeviceAPIService.SetAuthCode((string)desiredProperties["FacadeAuthCode"]);
                }

                Logger.Log("Desired property changed", LogLevel.Debug);
            }
            catch (AggregateException ex)
            {
                foreach (var exception in ex.InnerExceptions)
                {
                    Logger.Log($"Error when receiving desired property: {exception}", LogLevel.Error);
                }

                throw;
            }
            catch (Exception ex)
            {
                Logger.Log($"Error when receiving desired property: {ex.Message}", LogLevel.Error);
                throw;
            }

            return Task.CompletedTask;
        }

        async Task IPacketForwarder.SendDownstreamAsync(DownlinkPktFwdMessage downstreamMessage)
        {
            try
            {
                if (downstreamMessage?.Txpk != null)
                {
                    var jsonMsg = JsonConvert.SerializeObject(downstreamMessage);
                    var messageByte = Encoding.UTF8.GetBytes(jsonMsg);
                    var token = await GetTokenAsync();
                    var pyld = new PhysicalPayload(token, PhysicalIdentifier.PullResp, messageByte);
                    if (this.pullAckRemoteLoRaAggregatorPort != 0 && !string.IsNullOrEmpty(this.pullAckRemoteLoRaAddress))
                    {
                        Logger.Log("UDP", $"sending message with ID {ConversionHelper.ByteArrayToString(token)}, to {this.pullAckRemoteLoRaAddress}:{this.pullAckRemoteLoRaAggregatorPort}", LogLevel.Debug);
                        await UdpSendMessageAsync(pyld.GetMessage(), this.pullAckRemoteLoRaAddress, this.pullAckRemoteLoRaAggregatorPort);
                        Logger.Log("UDP", $"message sent with ID {ConversionHelper.ByteArrayToString(token)}", LogLevel.Debug);
                    }
                    else
                    {
                        Logger.Log(
                            "UDP",
                            "waiting for first pull_ack message from the packet forwarder. The received message was discarded as the network server is still starting.",
                            LogLevel.Debug);
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types. Packet forwarder is going away
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Logger.Log("UDP", $"error processing the message {ex.Message}, {ex.StackTrace}", LogLevel.Error);
            }
        }

        private void SetClassCMessageSender(DefaultClassCDevicesMessageSender classCMessageSender) => this.classCMessageSender = classCMessageSender;

        public void Dispose()
        {
            this.udpClient?.Dispose();
            this.udpClient = null;
            this.messageDispatcher?.Dispose();
            this.RndKeysGenerator?.Dispose();
        }
    }
}
