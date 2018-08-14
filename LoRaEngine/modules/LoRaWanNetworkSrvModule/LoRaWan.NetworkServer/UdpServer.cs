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
           
        ModuleClient ioTHubModuleClient;

        static UdpClient udpClient;

        private static IPAddress remoteLoRaAggregatorIp;
        private static int remoteLoRaAggregatorPort;

        public async Task RunServer()
        {

            await InitCallBack();
         
            await RunUdpListener();

        }

        public static async Task UdpSendMessage(byte[] messageToSend)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                await udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp.ToString(), remoteLoRaAggregatorPort);
                //Console.WriteLine($"UDP message sent on port: {remoteLoRaAggregatorPort}");
            }
        }

        async Task RunUdpListener()
        {


            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            udpClient = new UdpClient(endPoint);

            Console.WriteLine($"LoRaWAN server started on port {PORT}");
                 

            while (true)
            {
                UdpReceiveResult receivedResults = await udpClient.ReceiveAsync();

                //Console.WriteLine($"UDP message received ({receivedResults.Buffer.Length} bytes) from port: {receivedResults.RemoteEndPoint.Port}");

             
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
                    Console.WriteLine($"Error processing the message {ex.Message}");
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

                    Console.WriteLine("Getting properties from module twin...");


                    var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
                    var moduleTwinCollection = moduleTwin.Properties.Desired;

                    try
                    {
                        LoraDeviceInfoManager.FacadeServerUrl = moduleTwinCollection["FacadeServerUrl"];
                        Console.WriteLine($"Facade function url: {LoraDeviceInfoManager.FacadeServerUrl}");

                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Console.WriteLine("Module twin FacadeServerName not exist");
                    }
                    try
                    {
                        LoraDeviceInfoManager.FacadeAuthCode = moduleTwinCollection["FacadeAuthCode"];
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        Console.WriteLine("Module twin facadeAuthCode not exist");
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


               
                // Attach callback for Twin desired properties updates
              
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Initialization failed with error: {ex.Message}.\nWaiting for update desired property 'FacadeServerName' and 'FacadeAuthCode'.");
               
            }
        }

        private static async Task<MethodResponse> ClearCache(MethodRequest methodRequest, object userContext)
        {
            Cache.Clear();

            Console.WriteLine("Cache cleared");

            return new MethodResponse(200);
        }

        Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

               
                if (desiredProperties["FacadeServerUrl"] != null)
                    LoraDeviceInfoManager.FacadeServerUrl = desiredProperties["FacadeServerUrl"];

                if (desiredProperties["FacadeAuthCode"] != null)
                    LoraDeviceInfoManager.FacadeAuthCode = desiredProperties["FacadeAuthCode"];

            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
               
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
            return Task.CompletedTask;
        }

       
        public void Dispose()
        {

        }
    }

}
