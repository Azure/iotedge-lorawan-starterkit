using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class UdpServer : IDisposable
    {
        const int PORT = 1680;
        int retryCount = 0;

        string connectionString;
        bool udpListenerRunning = false;
        UdpClient udpClient = null;
        DeviceClient ioTHubModuleClient = null;
        MessageProcessor messageProcessor = null;
        bool exit = false;

        public async Task RunServer(bool bypassCertVerification)
        {
            await InitCallBack(bypassCertVerification);

            if(!string.IsNullOrEmpty(connectionString))
            {
                _ = RunUdpListener();
            }
            else
            {
                Console.WriteLine("UDP Listener not started yet.");
            }

            while (!exit)
            {
                await Task.Delay(100);
            }
        }

        async Task RunUdpListener()
        {
            while (true) //Continious restart logic...
            {
                try
                {
                    Console.WriteLine($"#{++retryCount} attempt to start UDP Listener...");

                    IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
                    udpClient = new UdpClient(endPoint);

                    Console.WriteLine($"UDP Listener started on port {PORT}");
                    udpListenerRunning = true;

                    while (true)
                    {
                        UdpReceiveResult receivedResults = await udpClient.ReceiveAsync();
                        Console.WriteLine($"UDP message received ({receivedResults.Buffer.Length} bytes).");
                        messageProcessor = new MessageProcessor();
                        await messageProcessor.processMessage(receivedResults.Buffer, connectionString);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to start UDP Listener on port {PORT}: {ex.Message}");
                }

                Task.Delay(5000).Wait();
            }
        }

        async Task InitCallBack(bool bypassCertVerification)
        {
            try
            {
                Console.WriteLine("Preparing data...");
                string connectionStringFromModule = Environment.GetEnvironmentVariable("EdgeHubConnectionString");
                Console.WriteLine($"EdgeHubConnectionString: '{connectionStringFromModule}'");

                Console.WriteLine("Setting up MqttTransportSettings");
                MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
                // During dev you might want to bypass the cert verification. It is highly recommended to verify certs systematically in production
                if (bypassCertVerification)
                {
                    mqttSetting.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                }
                ITransportSettings[] settings = { mqttSetting };

                ioTHubModuleClient = DeviceClient.CreateFromConnectionString(connectionStringFromModule, settings);

                Console.WriteLine("Getting 'connstr' property from module twin...");
                string connectionStringFromTwin = null;
                var moduleTwin = await ioTHubModuleClient.GetTwinAsync();
                var moduleTwinCollection = moduleTwin.Properties.Desired;
                if (moduleTwinCollection["connstr"] != null)
                {
                    string twinValue = moduleTwinCollection["connstr"];
                    Console.WriteLine($"Received module twin 'connstr':{twinValue}");
                    connectionStringFromTwin = twinValue;
                }

                Console.WriteLine("Constructing new connection string to IoTHub");
                constructConnectionStringMask(connectionStringFromModule, connectionStringFromTwin);

                Console.WriteLine("Registering callback for module twin update...");
                // Attach callback for Twin desired properties updates
                await ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, null);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Initialization failed with error: {ex.Message}.\nWaiting for update desired property 'connstr'.");
                connectionString = null;
            }
        }

        Task onDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                string connectionStringFromTwin = null;
                if (desiredProperties["connstr"] != null) connectionStringFromTwin = desiredProperties["connstr"];

                string connectionStringFromModule = Environment.GetEnvironmentVariable("EdgeHubConnectionString");
                constructConnectionStringMask(connectionStringFromModule, connectionStringFromTwin);

                if(!udpListenerRunning)
                {
                    _ = RunUdpListener();
                }
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

        void constructConnectionStringMask(string connectionStringFromModule, string connectionStringFromTwin)
        {
            //Constructing new Connection String
            connectionString = string.Empty;
            connectionString += $"HostName={getVal("HostName", connectionStringFromModule)};";
            connectionString += $"GatewayHostName={getVal("GatewayHostName", connectionStringFromModule)};";
            connectionString += $"SharedAccessKeyName={getVal("SharedAccessKeyName", connectionStringFromTwin)};";
            connectionString += $"SharedAccessKey={getVal("SharedAccessKey", connectionStringFromTwin)};";

            string getVal(string key, string connStr)
            {
                if(string.IsNullOrEmpty(key))
                {
                    throw new Exception($"Key cannot be null");
                }

                if (string.IsNullOrEmpty(connStr))
                {
                    throw new Exception($"Connection string cannot be null");
                }

                string val = null;

                foreach (var keyVal in connStr.Split(';'))
                {
                    int splIndex = keyVal.IndexOf('=');
                    string k = keyVal.Substring(0, splIndex);
                    if (k.ToLower().Trim() == key.ToLower().Trim())
                    {
                        val = keyVal.Substring(splIndex + 1, keyVal.Length - splIndex - 1);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(val))
                {
                    throw new Exception($"Key '{key}' not found.");
                }
                else
                {
                    return val;
                }
            }
        }

        public void Dispose()
        {
            if(udpClient != null)
            {
                if(udpClient.Client != null && udpClient.Client.Connected)
                {
                    try { udpClient.Client.Disconnect(false); } catch (Exception ex) { Console.WriteLine($"Udp Client socket disconnecting error: {ex.Message}"); }
                    try { udpClient.Client.Close(); } catch (Exception ex) { Console.WriteLine($"Udp Client socket closing error: {ex.Message}"); }
                    try { udpClient.Client.Dispose(); } catch (Exception ex) { Console.WriteLine($"Udp Client socket disposing error: {ex.Message}"); }
                }

                try { udpClient.Close(); } catch (Exception ex) { Console.WriteLine($"Udp Client closing error: {ex.Message}"); }
                try { udpClient.Dispose(); } catch (Exception ex) { Console.WriteLine($"Udp Client disposing error: {ex.Message}"); }
            }

            if(ioTHubModuleClient != null)
            {
                try { ioTHubModuleClient.CloseAsync().Wait(); } catch (Exception ex) { Console.WriteLine($"IoTHub Module Client closing error: {ex.Message}"); }
                try { ioTHubModuleClient.Dispose(); } catch (Exception ex) { Console.WriteLine($"IoTHub Module Client disposing error: {ex.Message}"); }
            }

            if(messageProcessor != null)
            {
                try { messageProcessor.Dispose(); } catch (Exception ex) { Console.WriteLine($"Message Processor disposing error: {ex.Message}"); }
            }
        }
    }
}
