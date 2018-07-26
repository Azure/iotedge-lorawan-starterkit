using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class IoTHubSender : IDisposable
    {
        private DeviceClient deviceClient;
        
        private string DevEUI;

        private string PrimaryKey;


        public IoTHubSender(string DevEUI, string PrimaryKey)
        {
            this.DevEUI = DevEUI;
            this.PrimaryKey = PrimaryKey;

            CreateDeviceClient();

        }

        private void CreateDeviceClient()
        {
            if (deviceClient == null)
            {
                try
                {

                    string partConnection = createIoTHubConnectionString(false);
                    string deviceConnectionStr = $"{partConnection}DeviceId={DevEUI};SharedAccessKey={PrimaryKey}";

                    deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionStr, TransportType.Mqtt_Tcp_Only);

                    //we set the retry only when sending msgs
                    deviceClient.SetRetryPolicy(new NoRetry());

                    //if the server disconnects dispose the deviceclient and new one will be created when a new d2c msg comes in.
                    deviceClient.SetConnectionStatusChangesHandler((status, reason) =>
                    {
                        if (status == ConnectionStatus.Disconnected)
                        {
                            deviceClient.Dispose();
                            deviceClient = null;
                            Console.WriteLine("Connection closed by the server");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not create IoT Hub Device Client with error: {ex.Message}");
                }

            }
        }

        public async Task SendMessage(string strMessage)
        {

            if (!string.IsNullOrEmpty(strMessage))
            {

                try
                {
                    CreateDeviceClient();

                    //Enable retry for this send message
                    deviceClient.SetRetryPolicy(new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100)));
                    await deviceClient.SendEventAsync(new Message(UTF8Encoding.ASCII.GetBytes(strMessage)));

                    //in future retrive the c2d msg to be sent to the device
                    //var c2dMsg = await deviceClient.ReceiveAsync((TimeSpan.FromSeconds(2)));

                    //disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                    deviceClient.SetRetryPolicy(new NoRetry());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not send message to IoTHub/Edge with error: {ex.Message}");
                }

            }
        }

        private string createIoTHubConnectionString(bool enableGateway)
        {

            string connectionStringFromModule = Environment.GetEnvironmentVariable("EdgeHubConnectionString");

            //TODO remove the test connection
            if(connectionStringFromModule==null)
                connectionStringFromModule = "HostName=ronnietest.azure-devices.net;GatewayHostName=;";

            
            string connectionString = string.Empty;
            connectionString += $"HostName={getVal("HostName", connectionStringFromModule)};";

            if (enableGateway)
                connectionString += $"GatewayHostName={getVal("GatewayHostName", connectionStringFromModule)};";


            string getVal(string key, string connStr)
            {
                if (string.IsNullOrEmpty(key))
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

            return connectionString;



        }

        public void Dispose()
        {
            if (deviceClient != null)
            {
                try { deviceClient.Dispose(); } catch (Exception ex) { Console.WriteLine($"Device Client disposing error: {ex.Message}"); }
            }
        }
    }
}
