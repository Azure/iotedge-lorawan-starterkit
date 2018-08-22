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
           
        static ModuleClient ioTHubModuleClient;

        static UdpClient udpClient;

        private static IPAddress remoteLoRaAggregatorIp;
        private static int remoteLoRaAggregatorPort;

        public async Task RunServer()
        {
            Logger.Log( "Starting LoRaWAN Server...", Logger.LoggingLevel.Always);

            await InitCallBack();
         
            await RunUdpListener();

        }


        public static async Task UdpSendMessage(byte[] messageToSend)
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

            Logger.Log( $"LoRaWAN server started on port {PORT}", Logger.LoggingLevel.Always);
                 

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

               


                try
                {
                    MessageProcessor messageProcessor = new MessageProcessor();
                    _= messageProcessor.processMessage(receivedResults.Buffer);
                }
                catch (Exception ex)
                {
                    Logger.Log( $"Error processing the message {ex.Message}", Logger.LoggingLevel.Error);
                }
                   
            }
           

           
        }

        async Task InitCallBack()
        {
            try
            {
                ITransportSettings transportSettings = new AmqpTransportSettings(TransportType.Amqp_Tcp_Only);
             
                ITransportSettings[] settings = { transportSettings };

                //if running as Edge module
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IOTEDGE_APIVERSION")))
                {
                    ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);

                    Logger.Init(ioTHubModuleClient);

                    Logger.Log( "Getting properties from module twin...", Logger.LoggingLevel.Info);


                    var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
                    var moduleTwinCollection = moduleTwin.Properties.Desired;

                    try
                    {
                        LoraDeviceInfoManager.FacadeServerUrl = moduleTwinCollection["FacadeServerUrl"];
                        Logger.Log( $"Facade function url: {LoraDeviceInfoManager.FacadeServerUrl}", Logger.LoggingLevel.Always);

                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log( "Module twin FacadeServerName not exist", Logger.LoggingLevel.Error);
                    }
                    try
                    {
                        LoraDeviceInfoManager.FacadeAuthCode = moduleTwinCollection["FacadeAuthCode"];
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Logger.Log( "Module twin FacadeAuthCode does not exist", Logger.LoggingLevel.Error);
                    }

                    await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, null);

                    await ioTHubModuleClient.SetMethodHandlerAsync("ClearCache", ClearCache, null);

                   
                }
                //todo ronnie what to do when not running as edge?
                //running as non edge module for test and debugging
                else
                {              
                    LoraDeviceInfoManager.FacadeServerUrl = "http://localhost:7071/api/";
                    LoraDeviceInfoManager.FacadeAuthCode = "";
                }


               
                
              
            }
            catch (Exception ex)
            {
                Logger.Log( $"Initialization failed with error: {ex.Message}", Logger.LoggingLevel.Error);
               
            }
        }

        private static async Task<MethodResponse> ClearCache(MethodRequest methodRequest, object userContext)
        {
            Cache.Clear();

            Logger.Log( "Cache cleared", Logger.LoggingLevel.Info);

            return new MethodResponse(200);
        }

        Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
               
                



                if (desiredProperties["FacadeServerUrl"] != null)
                    LoraDeviceInfoManager.FacadeServerUrl = desiredProperties["FacadeServerUrl"];

                if (desiredProperties["FacadeAuthCode"] != null)
                    LoraDeviceInfoManager.FacadeAuthCode = desiredProperties["FacadeAuthCode"];

                Logger.Log("Desired property changed", Logger.LoggingLevel.Info);

            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    
                    Logger.Log( $"Error when receiving desired property: {exception}", Logger.LoggingLevel.Error);
                }
            }
            catch (Exception ex)
            {
               
                Logger.Log( $"Error when receiving desired property: {ex.Message}", Logger.LoggingLevel.Error);
            }
            return Task.CompletedTask;
        }

       
        public void Dispose()
        {

        }
    }

}
