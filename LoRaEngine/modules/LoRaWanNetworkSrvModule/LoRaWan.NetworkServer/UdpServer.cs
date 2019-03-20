// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Defines udp Server communicating with packet forwarder
    /// </summary>
    public class UdpServer : IDisposable, IPacketForwarder
    {
        const int PORT = 1680;
        private readonly NetworkServerConfiguration configuration;
        private readonly MessageDispatcher messageDispatcher;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        readonly SemaphoreSlim randomLock = new SemaphoreSlim(1);
        readonly Random random = new Random();

        private IClassCDeviceMessageSender classCMessageSender;
        private ModuleClient ioTHubModuleClient;
        private volatile int pullAckRemoteLoRaAggregatorPort = 0;
        private volatile string pullAckRemoteLoRaAddress = null;
        UdpClient udpClient;

        private async Task<byte[]> GetTokenAsync()
        {
            try
            {
                await this.randomLock.WaitAsync();
                byte[] token = new byte[2];
                this.random.NextBytes(token);
                return token;
            }
            finally
            {
                this.randomLock.Release();
            }
        }

        // Creates a new instance of UdpServer
        public static UdpServer Create()
        {
            var configuration = NetworkServerConfiguration.CreateFromEnviromentVariables();

            var loRaDeviceAPIService = new LoRaDeviceAPIService(configuration, new ServiceFacadeHttpClientProvider(configuration, ApiVersion.LatestVersion));
            var frameCounterStrategyProvider = new LoRaDeviceFrameCounterUpdateStrategyProvider(configuration.GatewayID, loRaDeviceAPIService);
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

        public async Task RunServer()
        {
            Logger.LogAlways("Starting LoRaWAN Server...");

            await this.InitCallBack();

            await this.RunUdpListener();
        }

        async Task UdpSendMessageAsync(byte[] messageToSend, string remoteLoRaAggregatorIp, int remoteLoRaAggregatorPort)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                await this.udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp, remoteLoRaAggregatorPort);
            }
        }

        async Task RunUdpListener()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            this.udpClient = new UdpClient(endPoint);

            Logger.LogAlways($"LoRaWAN server started on port {PORT}");

            while (true)
            {
                UdpReceiveResult receivedResults = await this.udpClient.ReceiveAsync();
                var startTimeProcessing = DateTime.UtcNow;

                switch (PhysicalPayload.GetIdentifierFromPayload(receivedResults.Buffer))
                {
                    // In this case we have a keep-alive PULL_DATA packet we don't need to start the engine and can return immediately a response to the challenge
                    case PhysicalIdentifier.PULL_DATA:
                        if (this.pullAckRemoteLoRaAggregatorPort == 0)
                        {
                            this.pullAckRemoteLoRaAggregatorPort = receivedResults.RemoteEndPoint.Port;
                            this.pullAckRemoteLoRaAddress = receivedResults.RemoteEndPoint.Address.ToString();
                        }

                        this.SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PULL_ACK, receivedResults.RemoteEndPoint);
                        break;

                    // This is a PUSH_DATA (upstream message).
                    case PhysicalIdentifier.PUSH_DATA:
                        this.SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PUSH_ACK, receivedResults.RemoteEndPoint);
                        this.DispatchMessages(receivedResults.Buffer, startTimeProcessing);

                        break;

                    // This is a ack to a transmission we did previously
                    case PhysicalIdentifier.TX_ACK:
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
                                    "packet with id {0} had a problem to be transmitted over the air :{1}",
                                    receivedResults.Buffer.Length > 2 ? ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2)) : string.Empty,
                                    receivedResults.Buffer.Length > 12 ? Encoding.UTF8.GetString(receivedResults.Buffer.RangeSubset(12, receivedResults.Buffer.Length - 12)) : string.Empty);
                            Logger.Log("UDP", logMsg, LogLevel.Error);
                        }

                        break;

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
                List<Rxpk> messageRxpks = Rxpk.CreateRxpk(buffer);
                if (messageRxpks != null)
                {
                    foreach (var rxpk in messageRxpks)
                    {
                        this.messageDispatcher.DispatchRequest(new LoRaRequest(rxpk, this, startTimeProcessing));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("UDP", $"failed to dispatch messages: {ex.Message}", LogLevel.Error);
            }
        }

        private void SendAcknowledgementMessage(UdpReceiveResult receivedResults, byte messageType, IPEndPoint remoteEndpoint)
        {
            byte[] response = new byte[4]
            {
                receivedResults.Buffer[0],
                receivedResults.Buffer[1],
                receivedResults.Buffer[2],
                messageType
            };
            _ = this.UdpSendMessageAsync(response, remoteEndpoint.Address.ToString(), remoteEndpoint.Port);
        }

        async Task InitCallBack()
        {
            try
            {
                ITransportSettings transportSettings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);

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
                        this.loRaDeviceAPIService.SetURL(moduleTwinCollection["FacadeServerUrl"].Value as string);
                        Logger.LogAlways($"Facade function url: {this.loRaDeviceAPIService.URL}");
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeServerUrl property does not exist", LogLevel.Error);
                        throw e;
                    }

                    try
                    {
                        this.loRaDeviceAPIService.SetAuthCode(moduleTwinCollection["FacadeAuthCode"].Value as string);
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeAuthCode does not exist", LogLevel.Error);
                        throw e;
                    }

                    await this.ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(this.OnDesiredPropertiesUpdate, null);

                    await this.ioTHubModuleClient.SetMethodDefaultHandlerAsync(this.OnDirectMethodCalled, null);
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
            }
            catch (Exception ex)
            {
                Logger.Log($"Initialization failed with error: {ex.Message}", LogLevel.Error);
                throw ex;
            }

            // Report Log level
            Logger.LogAlways($"Log Level: {(LogLevel)Logger.LoggerLevel}");
        }

        async Task<MethodResponse> OnDirectMethodCalled(MethodRequest methodRequest, object userContext)
        {
            if (string.Equals("clearcache", methodRequest.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                return await this.ClearCache(methodRequest, userContext);
            }
            else if (string.Equals(Constants.CLOUD_TO_DEVICE_DECODER_ELEMENT_NAME, methodRequest.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                return await this.SendCloudToDeviceMessageAsync(methodRequest);
            }

            Logger.Log($"Unknown direct method called: {methodRequest?.Name}", LogLevel.Error);

            return new MethodResponse(404);
        }

        private async Task<MethodResponse> SendCloudToDeviceMessageAsync(MethodRequest methodRequest)
        {
            if (this.classCMessageSender == null)
            {
                return new MethodResponse((int)HttpStatusCode.NotFound);
            }

            var c2d = JsonConvert.DeserializeObject<ReceivedLoRaCloudToDeviceMessage>(methodRequest.DataAsJson);
            Logger.Log(c2d.DevEUI, $"received cloud to device message from direct method: {methodRequest.DataAsJson}", LogLevel.Debug);

            CancellationToken cts = CancellationToken.None;
            if (methodRequest.ResponseTimeout.HasValue)
                cts = new CancellationTokenSource(methodRequest.ResponseTimeout.Value).Token;

            if (await this.classCMessageSender.SendAsync(c2d, cts))
            {
                return new MethodResponse((int)HttpStatusCode.OK);
            }
            else
            {
                return new MethodResponse((int)HttpStatusCode.BadRequest);
            }
        }

        private Task<MethodResponse> ClearCache(MethodRequest methodRequest, object userContext)
        {
            this.loRaDeviceRegistry.ResetDeviceCache();

            return Task.FromResult(new MethodResponse(200));
        }

        Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                if (desiredProperties.Contains("FacadeServerUrl"))
                {
                    this.loRaDeviceAPIService.SetURL((string)desiredProperties["FacadeServerUrl"]);
                }

                if (desiredProperties.Contains("FacadeAuthCode"))
                {
                    this.loRaDeviceAPIService.SetAuthCode((string)desiredProperties["FacadeAuthCode"]);
                }

                Logger.Log("Desired property changed", LogLevel.Debug);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Logger.Log($"Error when receiving desired property: {exception}", LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error when receiving desired property: {ex.Message}", LogLevel.Error);
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
                    var token = await this.GetTokenAsync();
                    PhysicalPayload pyld = new PhysicalPayload(token, PhysicalIdentifier.PULL_RESP, messageByte);
                    if (this.pullAckRemoteLoRaAggregatorPort != 0 && !string.IsNullOrEmpty(this.pullAckRemoteLoRaAddress))
                    {
                        Logger.Log("UDP", $"sending message with ID {ConversionHelper.ByteArrayToString(token)}, to {this.pullAckRemoteLoRaAddress}:{this.pullAckRemoteLoRaAggregatorPort}", LogLevel.Debug);
                        await this.UdpSendMessageAsync(pyld.GetMessage(), this.pullAckRemoteLoRaAddress, this.pullAckRemoteLoRaAggregatorPort);
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
            catch (Exception ex)
            {
                Logger.Log("UDP", $"error processing the message {ex.Message}, {ex.StackTrace}", LogLevel.Error);
            }
        }

        private void SetClassCMessageSender(DefaultClassCDevicesMessageSender classCMessageSender) => this.classCMessageSender = classCMessageSender;

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.udpClient?.Dispose();
                    this.udpClient = null;
                }

                this.disposedValue = true;
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}