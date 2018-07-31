using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
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

                    string partConnection = createIoTHubConnectionString();
                    string deviceConnectionStr = $"{partConnection}DeviceId={DevEUI};SharedAccessKey={PrimaryKey}";

                    deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionStr, TransportType.Amqp_Tcp_Only);

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

        public async Task SendMessageAsync(string strMessage)
        {

            if (!string.IsNullOrEmpty(strMessage))
            {

                try
                {
                    CreateDeviceClient();

                    //Enable retry for this send message
                    deviceClient.SetRetryPolicy(new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100)));
                    await deviceClient.SendEventAsync(new Message(UTF8Encoding.ASCII.GetBytes(strMessage)));

                    //disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                    deviceClient.SetRetryPolicy(new NoRetry());
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not send message to IoTHub/Edge with error: {ex.Message}");
                }

            }
        }
        public async Task UpdateFcntAsync(int FCntUp, int? FCntDown)
        {


            try
            {
                CreateDeviceClient();

                Console.WriteLine($"Updating twins...");

                //updating the framecount non blocking because not critical and takes quite a bit of time
                TwinCollection prop;
                if (FCntDown != null)
                    prop = new TwinCollection($"{{\"FCntUp\":{FCntUp},\"FCntDown\":{FCntDown}}}");
                else
                    prop = new TwinCollection($"{{\"FCntUp\":{FCntUp}}}");

                await deviceClient.UpdateReportedPropertiesAsync(prop);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not update twins with error: {ex.Message}");
            }

            
        }

        public async Task<Message> GetMessageAsync(TimeSpan timeout)
        {


            try
            {
                CreateDeviceClient();

                return await deviceClient.ReceiveAsync(timeout);



            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not retrive message to IoTHub/Edge with error: {ex.Message}");
                return null;
            }


        }

        public async Task CompleteAsync(Message message)
        {
          
            await deviceClient.CompleteAsync(message);          

        }

        public async Task AbandonAsync(Message message)
        {

            await deviceClient.AbandonAsync(message);

        }

        public async Task OpenAsync()
        {

            await deviceClient.OpenAsync();

        }


        private string createIoTHubConnectionString()
        {

            bool enableGateway=false;
            string connectionString = string.Empty;

            string hostName = Environment.GetEnvironmentVariable("IOTEDGE_IOTHUBHOSTNAME");
            string gatewayHostName = Environment.GetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME");

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENABLE_GATEWAY")))
                enableGateway = bool.Parse(Environment.GetEnvironmentVariable("ENABLE_GATEWAY"));
            

            if(string.IsNullOrEmpty(hostName))
            {
                Console.WriteLine("Environment variable IOTEDGE_IOTHUBHOSTNAME not found, creation of iothub connection not possible");
            }
            

            connectionString += $"HostName={hostName};";

            if (enableGateway)
            {
                connectionString += $"GatewayHostName={hostName};";                
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
