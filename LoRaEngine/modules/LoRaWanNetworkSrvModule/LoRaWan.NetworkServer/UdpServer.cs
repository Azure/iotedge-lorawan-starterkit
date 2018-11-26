//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class UdpServer : IDisposable
    {
        const int PORT = 1680;
        private readonly NetworkServerConfiguration configuration;
        private readonly LoraDeviceInfoManager loraDeviceInfoManager;
        ModuleClient ioTHubModuleClient;

        UdpClient udpClient;

        private IPAddress remoteLoRaAggregatorIp;
        private int remoteLoRaAggregatorPort;

        // Creates a new instance of UdpServer
        public static UdpServer Create()
        {
            var configuration = NetworkServerConfiguration.CreateFromEnviromentVariables();
            var loraDeviceInfoManager = new LoraDeviceInfoManager(configuration);
            return new UdpServer(configuration, loraDeviceInfoManager);
        }

        // Creates a new instance of UdpServer
        public UdpServer(NetworkServerConfiguration configuration, LoraDeviceInfoManager loraDeviceInfoManager)
        {
            this.configuration = configuration;
            this.loraDeviceInfoManager = loraDeviceInfoManager;
        }
        public async Task RunServer()
        {
            Logger.Log("Starting LoRaWAN Server...", Logger.LoggingLevel.Always);

            await InitCallBack();

            await RunUdpListener();

        }


        public async Task UdpSendMessage(byte[] messageToSend)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                await udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp.ToString(), remoteLoRaAggregatorPort);

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

                //Logger.Log($"UDP message received ({receivedResults.Buffer.Length} bytes) from port: {receivedResults.RemoteEndPoint.Port}");


                //Todo check that is an ack only, we could do a better check in a future verstion
                if (receivedResults.Buffer.Length == 12)
                {
                    remoteLoRaAggregatorIp = receivedResults.RemoteEndPoint.Address;
                    remoteLoRaAggregatorPort = receivedResults.RemoteEndPoint.Port;
                }

                MessageProcessor messageProcessor = new MessageProcessor(this.configuration, this.loraDeviceInfoManager);

                // Message processing runs in the background
                #pragma warning disable CS4014 
                Task.Run(async () => {
                    try
                    {
                        var resultMessage = await messageProcessor.ProcessMessageAsync(receivedResults.Buffer);
                        if (resultMessage != null && resultMessage.Length > 0)
                            await this.UdpSendMessage(resultMessage);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error processing the message {ex.Message}, {ex.StackTrace}", Logger.LoggingLevel.Error);
                    }
                });                    
                #pragma warning restore CS4014 
            }
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
                    
                    Logger.Init(new LoggerConfiguration{
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
                        this.loraDeviceInfoManager.FacadeServerUrl = moduleTwinCollection["FacadeServerUrl"];
                        Logger.Log($"Facade function url: {this.loraDeviceInfoManager.FacadeServerUrl}", Logger.LoggingLevel.Always);

                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log("Module twin FacadeServerName not exist", Logger.LoggingLevel.Error);
                        throw e;
                    }
                    try
                    {
                        this.loraDeviceInfoManager.FacadeAuthCode = moduleTwinCollection["FacadeAuthCode"];
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
                    Logger.Init(new LoggerConfiguration{
                        ModuleClient = null,
                        LogLevel = configuration.LogLevel,
                        LogToConsole = configuration.LogToConsole,
                        LogToHub = configuration.LogToHub,
                        LogToUdp = configuration.LogToUdp,
                        LogToUdpPort = configuration.LogToUdpPort,
                        LogToUdpAddress = configuration.LogToUdpAddress,
                    });

                    this.loraDeviceInfoManager.FacadeServerUrl = configuration.FacadeServerUrl;
                    this.loraDeviceInfoManager.FacadeAuthCode = configuration.FacadeAuthCode;
                }





            }
            catch (Exception ex)
            {
                Logger.Log($"Initialization failed with error: {ex.Message}", Logger.LoggingLevel.Error);
                throw ex;

            }
        }

        private static Task<MethodResponse> ClearCache(MethodRequest methodRequest, object userContext)
        {
            Cache.Clear();

            Logger.Log("Cache cleared", Logger.LoggingLevel.Info);

            return Task.FromResult(new MethodResponse(200));
        }

        Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {


                if (desiredProperties["FacadeServerUrl"] != null)
                    this.loraDeviceInfoManager.FacadeServerUrl = desiredProperties["FacadeServerUrl"];

                if (desiredProperties["FacadeAuthCode"] != null)
                    this.loraDeviceInfoManager.FacadeAuthCode = desiredProperties["FacadeAuthCode"];

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


        public void Dispose()
        {

        }
    }

}