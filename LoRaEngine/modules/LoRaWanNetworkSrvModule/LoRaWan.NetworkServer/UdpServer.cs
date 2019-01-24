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
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Newtonsoft.Json;
    using static LoRaWan.Logger;

    /// <summary>
    /// Defines udp Server communicating with packet forwarder
    /// </summary>
    public class UdpServer : IDisposable
    {
        const int PORT = 1680;
        private readonly NetworkServerConfiguration configuration;

        private readonly MessageProcessor messageProcessorV2;
        private readonly LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly ILoRaDeviceRegistry loRaDeviceRegistry;
        readonly SemaphoreSlim randomLock = new SemaphoreSlim(1);
        readonly Random random = new Random();

        ModuleClient ioTHubModuleClient;
        private volatile int pullAckRemoteLoRaAggregatorPort = 0;
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

            var loRaDeviceFactory = new LoRaDeviceFactory(configuration);
            var loRaDeviceAPIService = new LoRaDeviceAPIService(configuration, new ServiceFacadeHttpClientProvider(configuration, ApiVersion.LatestVersion));
            var loRaDeviceRegistry = new LoRaDeviceRegistry(configuration, new MemoryCache(new MemoryCacheOptions()), loRaDeviceAPIService, loRaDeviceFactory);
            var frameCounterStrategyFactory = new LoRaDeviceFrameCounterUpdateStrategyFactory(configuration.GatewayID, loRaDeviceAPIService);
            var messageProcessor = new MessageProcessor(configuration, loRaDeviceRegistry, frameCounterStrategyFactory, new LoRaPayloadDecoder());
            return new UdpServer(configuration, messageProcessor, loRaDeviceAPIService, loRaDeviceRegistry);
        }

        // Creates a new instance of UdpServer
        public UdpServer(
            NetworkServerConfiguration configuration,
            MessageProcessor messageProcessor,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceRegistry loRaDeviceRegistry)
        {
            this.configuration = configuration;
            this.messageProcessorV2 = messageProcessor;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
        }

        public async Task RunServer()
        {
            Logger.Log("Starting LoRaWAN Server...", Logger.LoggingLevel.Always);

            await this.InitCallBack();

            await this.RunUdpListener();
        }

        public async Task UdpSendMessage(byte[] messageToSend, string remoteLoRaAggregatorIp, int remoteLoRaAggregatorPort)
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

            Logger.Log($"LoRaWAN server started on port {PORT}", Logger.LoggingLevel.Always);

            while (true)
            {
                UdpReceiveResult receivedResults = await this.udpClient.ReceiveAsync();
                var startTimeProcessing = DateTime.UtcNow;
                // Logger.Log($"UDP message received ({receivedResults.Buffer[3]}) from port: {receivedResults.RemoteEndPoint.Port} and IP: {receivedResults.RemoteEndPoint.Address.ToString()}",LoggingLevel.Always);
                switch (PhysicalPayload.GetIdentifierFromPayload(receivedResults.Buffer))
                {
                    // In this case we have a keep-alive PULL_DATA packet we don't need to start the engine and can return immediately a response to the challenge
                    case PhysicalIdentifier.PULL_DATA:
                        if (this.pullAckRemoteLoRaAggregatorPort == 0)
                        {
                            this.pullAckRemoteLoRaAggregatorPort = receivedResults.RemoteEndPoint.Port;
                        }

                        this.SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PULL_ACK, receivedResults.RemoteEndPoint);
                        break;

                    // This is a PUSH_DATA (upstream message).
                    case PhysicalIdentifier.PUSH_DATA:
                        this.SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PUSH_ACK, receivedResults.RemoteEndPoint);

                        // Message processing runs in the background
                        var remoteEndPointAddress = receivedResults.RemoteEndPoint.Address.ToString();
                        _ = Task.Run(async () =>
                        {
                            List<Rxpk> messageRxpks = Rxpk.CreateRxpk(receivedResults.Buffer);
                            if (messageRxpks != null)
                            {
                                if (messageRxpks.Count == 1)
                                {
                                    await this.ProcessRxpkAsync(receivedResults.RemoteEndPoint.Address.ToString(), messageRxpks[0], startTimeProcessing);
                                }
                                else if (messageRxpks.Count > 1)
                                {
                                    Task toWait = null;
                                    for (int i = 0; i < messageRxpks.Count; i++)
                                    {
                                        var t = this.ProcessRxpkAsync(remoteEndPointAddress, messageRxpks[i], startTimeProcessing);
                                        if (toWait == null)
                                            toWait = t;
                                    }

                                    await toWait;
                                }
                            }
                        });

                        break;

                    // This is a ack to a transmission we did previously
                    case PhysicalIdentifier.TX_ACK:
                        if (receivedResults.Buffer.Length == 12)
                        {
                            Logger.Log(
                                "UDP",
                                $"Packet with id {ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2))} successfully transmitted by the aggregator",
                                LoggingLevel.Full);
                        }
                        else
                        {
                            var logMsg = string.Format(
                                    "Packet with id {0} had a problem to be transmitted over the air :{1}",
                                    receivedResults.Buffer.Length > 2 ? ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2)) : string.Empty,
                                    receivedResults.Buffer.Length > 12 ? Encoding.UTF8.GetString(receivedResults.Buffer.RangeSubset(12, receivedResults.Buffer.Length - 12)) : string.Empty);
                            Logger.Log("UDP", logMsg, LoggingLevel.Error);
                        }

                        break;

                    default:
                        Logger.Log("UDP", "Unknown packet type or length being received", LoggingLevel.Error);
                        break;
                }
            }
        }

        private async Task ProcessRxpkAsync(string remoteIp, Rxpk rxpk, DateTime startTimeProcessing)
        {
            try
            {
                var downstreamMessage = await this.messageProcessorV2.ProcessMessageAsync(rxpk, startTimeProcessing);
                if (downstreamMessage?.txpk != null)
                {
                    var jsonMsg = JsonConvert.SerializeObject(downstreamMessage);
                    var messageByte = Encoding.UTF8.GetBytes(jsonMsg);
                    var token = await this.GetTokenAsync();
                    PhysicalPayload pyld = new PhysicalPayload(token, PhysicalIdentifier.PULL_RESP, messageByte);
                    if (this.pullAckRemoteLoRaAggregatorPort != 0)
                    {
                        await this.UdpSendMessage(pyld.GetMessage(), remoteIp, this.pullAckRemoteLoRaAggregatorPort);
                        Logger.Log("UDP", $"message sent with ID {ConversionHelper.ByteArrayToString(token)}", Logger.LoggingLevel.Info);
                    }
                    else
                    {
                        Logger.Log(
                            "UDP",
                            "Waiting for first pull_ack message from the packet forwarder. The received message was discarded as the network server is still starting.",
                            Logger.LoggingLevel.Full);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error processing the message {ex.Message}, {ex.StackTrace}", Logger.LoggingLevel.Error);
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
            _ = this.UdpSendMessage(response, remoteEndpoint.Address.ToString(), remoteEndpoint.Port);
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
                        LogLevel = this.configuration.LogLevel,
                        LogToConsole = this.configuration.LogToConsole,
                        LogToHub = this.configuration.LogToHub,
                        LogToUdp = this.configuration.LogToUdp,
                        LogToUdpPort = this.configuration.LogToUdpPort,
                        LogToUdpAddress = this.configuration.LogToUdpAddress,
                    });

                    if (this.configuration.IoTEdgeTimeout > 0)
                    {
                        this.ioTHubModuleClient.OperationTimeoutInMilliseconds = this.configuration.IoTEdgeTimeout;
                        Logger.Log($"Changing timeout to {this.ioTHubModuleClient.OperationTimeoutInMilliseconds} ms", Logger.LoggingLevel.Info);
                    }

                    Logger.Log("Getting properties from module twin...", Logger.LoggingLevel.Info);

                    var moduleTwin = await this.ioTHubModuleClient.GetTwinAsync();
                    var moduleTwinCollection = moduleTwin.Properties.Desired;

                    try
                    {
                        this.loRaDeviceAPIService.SetURL((string)moduleTwinCollection["FacadeServerUrl"]);
                        Logger.Log($"Facade function url: {this.loRaDeviceAPIService.URL}", Logger.LoggingLevel.Always);
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeServerName not exist", Logger.LoggingLevel.Error);
                        throw e;
                    }

                    try
                    {
                        this.loRaDeviceAPIService.SetAuthCode((string)moduleTwinCollection["FacadeAuthCode"]);
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeAuthCode does not exist", Logger.LoggingLevel.Error);
                        throw e;
                    }

                    await this.ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(this.OnDesiredPropertiesUpdate, null);

                    await this.ioTHubModuleClient.SetMethodHandlerAsync("ClearCache", this.ClearCache, null);
                }

                // running as non edge module for test and debugging
                else
                {
                    Logger.Init(new LoggerConfiguration
                    {
                        ModuleClient = null,
                        LogLevel = this.configuration.LogLevel,
                        LogToConsole = this.configuration.LogToConsole,
                        LogToHub = this.configuration.LogToHub,
                        LogToUdp = this.configuration.LogToUdp,
                        LogToUdpPort = this.configuration.LogToUdpPort,
                        LogToUdpAddress = this.configuration.LogToUdpAddress,
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Initialization failed with error: {ex.Message}", Logger.LoggingLevel.Error);
                throw ex;
            }
        }

        private Task<MethodResponse> ClearCache(MethodRequest methodRequest, object userContext)
        {
            this.loRaDeviceRegistry.ResetDeviceCache();

            Logger.Log("Cache cleared", Logger.LoggingLevel.Info);

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

                Logger.Log("Desired property changed", Logger.LoggingLevel.Info);
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Logger.Log($"Error when receiving desired property: {exception}", Logger.LoggingLevel.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error when receiving desired property: {ex.Message}", Logger.LoggingLevel.Error);
            }

            return Task.CompletedTask;
        }

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