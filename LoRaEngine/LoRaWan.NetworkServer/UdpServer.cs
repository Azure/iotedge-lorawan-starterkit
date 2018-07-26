using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net;
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

        public async Task RunServer(bool bypassCertVerification)
        {

            await InitCallBack(bypassCertVerification);
         
            await RunUdpListener();

        }

        public static async Task UdpSendMessage(byte[] messageToSend)
        {
            if (messageToSend!=null && messageToSend.Length != 0)
                await udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp.ToString(), remoteLoRaAggregatorPort);
        }

        async Task RunUdpListener()
        {


            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            udpClient = new UdpClient(endPoint);

            Console.WriteLine($"UDP Listener started on port {PORT}");
                 

            while (true)
            {
                UdpReceiveResult receivedResults = await udpClient.ReceiveAsync();

                Console.WriteLine($"UDP message received ({receivedResults.Buffer.Length} bytes).");

                //connection
                if (remoteLoRaAggregatorIp == null)
                {
                    remoteLoRaAggregatorIp = receivedResults.RemoteEndPoint.Address;
                    remoteLoRaAggregatorPort = receivedResults.RemoteEndPoint.Port; 
                        
                }


                try
                {
                    MessageProcessor messageProcessor = new MessageProcessor();
                     messageProcessor.processMessage(receivedResults.Buffer);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing the message {ex.Message}");
                }
                   
            }
           

           
        }

        async Task InitCallBack(bool bypassCertVerification)
        {
            try
            {
                
   

                Console.WriteLine("Setting up MqttTransportSettings");
                MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                // During dev you might want to bypass the cert verification. It is highly recommended to verify certs systematically in production
                if (bypassCertVerification)
                {
                    mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                }
                ITransportSettings[] settings = { mqttSetting };

                //if running as Edge module
                if (Environment.GetEnvironmentVariable("EdgeHubConnectionString") != null)
                {
                    ioTHubModuleClient = ModuleClient.CreateFromEnvironment(settings);

                    Console.WriteLine("Getting properties from module twin...");


                    var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
                    var moduleTwinCollection = moduleTwin.Properties.Desired;

                    try
                    {
                        LoraDeviceInfoManager.FacadeServerUrl = moduleTwinCollection["FacadeServerUrl"];
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
                }
                //running as non edge module for test and debugging
                else
                {

                    //TODO ronnie remove the test keys
                    //LoraDeviceInfoManager.FacadeServerUrl = "https://lorafacade.azurewebsites.net/api/";
                    LoraDeviceInfoManager.FacadeServerUrl = "http://localhost:7071/api/";
                    LoraDeviceInfoManager.FacadeAuthCode = "";
                }


                Console.WriteLine("Registering callback for module twin update...");
                // Attach callback for Twin desired properties updates
               // await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Initialization failed with error: {ex.Message}.\nWaiting for update desired property 'FacadeServerName' and 'FacadeAuthCode'.");
               
            }
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
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
            return Task.CompletedTask;
        }



        public void Dispose()
        {

        }
    }
}
