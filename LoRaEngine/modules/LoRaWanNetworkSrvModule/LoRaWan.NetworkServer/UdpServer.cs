//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#define USE_MESSAGE_PROCESSOR_V2

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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static LoRaWan.Logger;


namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Defines udp Server communicating with packet forwarder
    /// </summary>
    public class UdpServer : IDisposable
    {
        const int PORT = 1680;
        private readonly NetworkServerConfiguration configuration;

#if USE_MESSAGE_PROCESSOR_V2
        private readonly V2.MessageProcessor messageProcessorV2;
        private readonly V2.LoRaDeviceAPIServiceBase loRaDeviceAPIService;
        private readonly V2.ILoRaDeviceRegistry loRaDeviceRegistry;
#else
        private readonly LoraDeviceInfoManager loraDeviceInfoManager;
#endif

        ModuleClient ioTHubModuleClient;
        private int pullAckRemoteLoRaAggregatorPort = 0;
        UdpClient udpClient;

        SemaphoreSlim randomLock = new SemaphoreSlim(1);
        Random random = new Random();
        private async Task<byte[]> GetTokenAsync()
        {
            try
            {
                await randomLock.WaitAsync();
                byte[] token = new byte[2];
                random.NextBytes(token);
                return token;
            }
            finally
            {
                randomLock.Release();
            }
        }

        // Creates a new instance of UdpServer
        public static UdpServer Create()
        {
            var configuration = NetworkServerConfiguration.CreateFromEnviromentVariables();
#if USE_MESSAGE_PROCESSOR_V2
            var loRaDeviceFactory = new V2.LoRaDeviceFactory(configuration);
            var loRaDeviceAPIService = new V2.LoRaDeviceAPIService(configuration, new ServiceFacadeHttpClientProvider(configuration, ApiVersion.LatestVersion));
            var loRaDeviceRegistry = new V2.LoRaDeviceRegistry(configuration, new MemoryCache(new MemoryCacheOptions()), loRaDeviceAPIService, loRaDeviceFactory);
            var frameCounterStrategyFactory = new V2.LoRaDeviceFrameCounterUpdateStrategyFactory(configuration.GatewayID, loRaDeviceAPIService);
            var messageProcessor = new V2.MessageProcessor(configuration, loRaDeviceRegistry, frameCounterStrategyFactory, new V2.LoRaPayloadDecoder());
            return new UdpServer(configuration, messageProcessor, loRaDeviceAPIService, loRaDeviceRegistry);
#else
            var loraDeviceInfoManager = new LoraDeviceInfoManager(configuration, new ServiceFacadeHttpClientProvider(configuration, ApiVersion.LatestVersion));
            return new UdpServer(configuration, loraDeviceInfoManager);
#endif
        }

#if USE_MESSAGE_PROCESSOR_V2
        // Creates a new instance of UdpServer
        public UdpServer(NetworkServerConfiguration configuration, 
            V2.MessageProcessor messageProcessor, 
            V2.LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            V2.ILoRaDeviceRegistry loRaDeviceRegistry)
        {
            this.configuration = configuration;
            this.messageProcessorV2 = messageProcessor;
            this.loRaDeviceAPIService = loRaDeviceAPIService;
            this.loRaDeviceRegistry = loRaDeviceRegistry;
        }
#else
        // Creates a new instance of UdpServer
        public UdpServer(NetworkServerConfiguration configuration, LoraDeviceInfoManager loraDeviceInfoManager)
        {
            this.configuration = configuration;
            this.loraDeviceInfoManager = loraDeviceInfoManager;
        }
#endif

        public async Task RunServer()
        {
            Logger.Log("Starting LoRaWAN Server...", Logger.LoggingLevel.Always);

            await InitCallBack();

            await RunUdpListener();

        }


        public async Task UdpSendMessage(byte[] messageToSend, string remoteLoRaAggregatorIp, int remoteLoRaAggregatorPort)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                await udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp, remoteLoRaAggregatorPort);

            }
        }

        async Task RunUdpListener()
        {


            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            udpClient = new UdpClient(endPoint);

            Logger.Log($"LoRaWAN server started on port {PORT}", Logger.LoggingLevel.Always);


            while (true)
            {
                UdpReceiveResult receivedResults = await udpClient.ReceiveAsync();
                var startTimeProcessing= DateTime.UtcNow;
                //Logger.Log($"UDP message received ({receivedResults.Buffer[3]}) from port: {receivedResults.RemoteEndPoint.Port} and IP: {receivedResults.RemoteEndPoint.Address.ToString()}",LoggingLevel.Always);


                switch (PhysicalPayload.GetIdentifierFromPayload(receivedResults.Buffer))
                {
                    //In this case we have a keep-alive PULL_DATA packet we don't need to start the engine and can return immediately a response to the challenge
                    case PhysicalIdentifier.PULL_DATA:
                        if (pullAckRemoteLoRaAggregatorPort == 0)
                        {
                            pullAckRemoteLoRaAggregatorPort = receivedResults.RemoteEndPoint.Port;
                        }
                        SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PULL_ACK, receivedResults.RemoteEndPoint);
                        break;
                    //This is a PUSH_DATA (upstream message).
                    case PhysicalIdentifier.PUSH_DATA:
                        SendAcknowledgementMessage(receivedResults, (int)PhysicalIdentifier.PUSH_ACK, receivedResults.RemoteEndPoint);
                        // Message processing runs in the background
#pragma warning disable CS4014
                        Task.Run(async () =>
                        {
                            List<Rxpk> messageRxpks = Rxpk.CreateRxpk(receivedResults.Buffer);
                            if (messageRxpks != null  )
                            {
                                if (messageRxpks.Count == 1 )
                                {
                                     await ProcessRxpkAsync(receivedResults.RemoteEndPoint.Address.ToString(), messageRxpks[0], startTimeProcessing);
                                }
                                else if(messageRxpks.Count > 1) {                    
                                   for (int i = 0; i< messageRxpks.Count-1;i++)
                                    {
                                         ProcessRxpkAsync(receivedResults.RemoteEndPoint.Address.ToString(), messageRxpks[i], startTimeProcessing);
                                    }
                                    await ProcessRxpkAsync(receivedResults.RemoteEndPoint.Address.ToString(), messageRxpks[messageRxpks.Count-1], startTimeProcessing);
                                }
                            }
                        });
#pragma warning restore CS4014

                        break;
                    //This is a ack to a transmission we did previously
                    case PhysicalIdentifier.TX_ACK:
                        if (receivedResults.Buffer.Length == 12)
                        {
                            Logger.Log("UDP", String.Format("Packet with id {0} successfully transmitted by the aggregator",
                            ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2)))
                            , LoggingLevel.Full);
                        }
                        else
                        {
                            Logger.Log("UDP", String.Format("Packet with id {0} had a problem to be transmitted over the air :{1}",
                            receivedResults.Buffer.Length > 2 ? ConversionHelper.ByteArrayToString(receivedResults.Buffer.RangeSubset(1, 2)) : "",
                            receivedResults.Buffer.Length > 12 ? Encoding.UTF8.GetString(receivedResults.Buffer.RangeSubset(12, receivedResults.Buffer.Length - 12)) : "")
                            , LoggingLevel.Error);
                        }
                        break;
                    default:
                        Logger.Log("UDP", "Unknown packet type or length being received", LoggingLevel.Error);
                        break;


                }


            }
        } 
        private async Task ProcessRxpkAsync(String remoteIp, Rxpk rxpk, DateTime startTimeProcessing)
        {
            try
            {
                MessageProcessor messageProcessor = new MessageProcessor(this.configuration, this.loraDeviceInfoManager,  startTimeProcessing);
                var downstreamMessage = await messageProcessor.ProcessMessageAsync(rxpk);
                if (downstreamMessage?.txpk != null)
                {
                    var jsonMsg = JsonConvert.SerializeObject(downstreamMessage);
                    var messageByte = Encoding.UTF8.GetBytes(jsonMsg);
                    var token = await GetTokenAsync();
                    PhysicalPayload pyld = new PhysicalPayload(token, PhysicalIdentifier.PULL_RESP, messageByte);
                    await this.UdpSendMessage(pyld.GetMessage(), remoteIp, pullAckRemoteLoRaAggregatorPort);
                    Logger.Log("UDP", String.Format("message sent with ID {0}",
                        ConversionHelper.ByteArrayToString(token)),
                        Logger.LoggingLevel.Full);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error processing the message {ex.Message}, {ex.StackTrace}", Logger.LoggingLevel.Error);
            }

        }

        private void SendAcknowledgementMessage(UdpReceiveResult receivedResults, byte messageType, IPEndPoint remoteEndpoint)
        {
            byte[] response = new byte[4]{
                            receivedResults.Buffer[0],
                            receivedResults.Buffer[1],
                            receivedResults.Buffer[2],
                            messageType
                            };
            _ = UdpSendMessage(response, remoteEndpoint.Address.ToString(), remoteEndpoint.Port);
        }


        async Task InitCallBack()
        {
            try
            {
                ITransportSettings transportSettings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);

                ITransportSettings[] settings = { transportSettings };

                //if running as Edge module
                if (configuration.RunningAsIoTEdgeModule)
                {
                    ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

                    Logger.Init(new LoggerConfiguration
                    {
                        ModuleClient = ioTHubModuleClient,
                        LogLevel = configuration.LogLevel,
                        LogToConsole = configuration.LogToConsole,
                        LogToHub = configuration.LogToHub,
                        LogToUdp = configuration.LogToUdp,
                        LogToUdpPort = configuration.LogToUdpPort,
                        LogToUdpAddress = configuration.LogToUdpAddress,
                    });

                    if (configuration.IoTEdgeTimeout > 0)
                    {
                        ioTHubModuleClient.OperationTimeoutInMilliseconds = configuration.IoTEdgeTimeout;
                        Logger.Log($"Changing timeout to {ioTHubModuleClient.OperationTimeoutInMilliseconds} ms", Logger.LoggingLevel.Info);
                    }

                    Logger.Log("Getting properties from module twin...", Logger.LoggingLevel.Info);


                    var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
                    var moduleTwinCollection = moduleTwin.Properties.Desired;

                    try
                    {
#if USE_MESSAGE_PROCESSOR_V2
                        this.loRaDeviceAPIService.SetURL((string)moduleTwinCollection["FacadeServerUrl"]);
                        Logger.Log($"Facade function url: {this.loRaDeviceAPIService.URL}", Logger.LoggingLevel.Always);
#else
                        this.loraDeviceInfoManager.FacadeServerUrl = moduleTwinCollection["FacadeServerUrl"];
                        Logger.Log($"Facade function url: {this.loraDeviceInfoManager.FacadeServerUrl}", Logger.LoggingLevel.Always);
#endif


                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeServerName not exist", Logger.LoggingLevel.Error);
                        throw e;
                    }
                    try
                    {
#if USE_MESSAGE_PROCESSOR_V2
                        this.loRaDeviceAPIService.SetAuthCode((string)moduleTwinCollection["FacadeAuthCode"]);
#else
                        this.loraDeviceInfoManager.FacadeAuthCode = moduleTwinCollection["FacadeAuthCode"];
#endif
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeAuthCode does not exist", Logger.LoggingLevel.Error);
                        throw e;
                    }

                    await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, null);

                    await ioTHubModuleClient.SetMethodHandlerAsync("ClearCache", ClearCache, null);


                }
                //running as non edge module for test and debugging
                else
                {
                    Logger.Init(new LoggerConfiguration
                    {
                        ModuleClient = null,
                        LogLevel = configuration.LogLevel,
                        LogToConsole = configuration.LogToConsole,
                        LogToHub = configuration.LogToHub,
                        LogToUdp = configuration.LogToUdp,
                        LogToUdpPort = configuration.LogToUdpPort,
                        LogToUdpAddress = configuration.LogToUdpAddress,
                    });

#if !USE_MESSAGE_PROCESSOR_V2
                    this.loraDeviceInfoManager.FacadeServerUrl = configuration.FacadeServerUrl;
                    this.loraDeviceInfoManager.FacadeAuthCode = configuration.FacadeAuthCode;
#endif
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
#if USE_MESSAGE_PROCESSOR_V2
            this.loRaDeviceRegistry.ResetDeviceCache();
#else
            Cache.Clear();
#endif

            Logger.Log("Cache cleared", Logger.LoggingLevel.Info);

            return Task.FromResult(new MethodResponse(200));
        }

        Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {


                if (desiredProperties["FacadeServerUrl"] != null)
                {
#if USE_MESSAGE_PROCESSOR_V2
                    this.loRaDeviceAPIService.SetURL((string)desiredProperties["FacadeServerUrl"]);
#else
                    this.loraDeviceInfoManager.FacadeServerUrl = desiredProperties["FacadeServerUrl"];
#endif
                }


                if (desiredProperties["FacadeAuthCode"] != null)
                {
#if USE_MESSAGE_PROCESSOR_V2
                    this.loRaDeviceAPIService.SetAuthCode((string)desiredProperties["FacadeAuthCode"]);
#else
                    this.loraDeviceInfoManager.FacadeAuthCode = desiredProperties["FacadeAuthCode"];
#endif
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

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.udpClient?.Dispose();
                    this.udpClient = null;
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

}