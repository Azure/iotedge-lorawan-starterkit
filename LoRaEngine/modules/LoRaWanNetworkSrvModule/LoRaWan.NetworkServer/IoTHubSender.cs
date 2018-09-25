//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
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

        public async Task<Twin> GetTwinAsync()
        {
            return await deviceClient.GetTwinAsync();
        }

        public async Task UpdateFcntAsync(int FCntUp, int? FCntDown, bool force = false)
        {
            try
            {
                //update the twins every 10
                if (FCntUp % 10 == 0 || force == true )
                {
                    CreateDeviceClient();
                    TwinCollection prop;
                    if (FCntDown != null)
                    {
                        prop = new TwinCollection($"{{\"FCntUp\":{FCntUp},\"FCntDown\":{FCntDown}}}");
                    }
                    else
                    {
                        prop = new TwinCollection($"{{\"FCntUp\":{FCntUp}}}");
                    }

                    await deviceClient.UpdateReportedPropertiesAsync(prop);

                    Logger.Log(DevEUI, $"twins updated {FCntUp}:{FCntDown}", Logger.LoggingLevel.Info);
                }

            }
            catch (Exception ex)
            {
                Logger.Log(DevEUI, $"could not update twins with error: {ex.Message}", Logger.LoggingLevel.Error);
            }


        }
        /// <summary>
        /// Method to update reported properties at OTAA time
        /// </summary>
        /// <param name="loraDeviceInfo"> the LoRa info to report</param>
        public async void UpdateReportedPropertiesOTAAasync(LoraDeviceInfo loraDeviceInfo)
        {
            CreateDeviceClient();
            TwinCollection reportedProperties = new TwinCollection();
            reportedProperties["NwkSKey"] = loraDeviceInfo.NwkSKey;
            reportedProperties["AppSKey"] = loraDeviceInfo.AppSKey;
            reportedProperties["DevEUI"] = loraDeviceInfo.DevEUI;
            reportedProperties["NetId"] = loraDeviceInfo.NetId;
            reportedProperties["FCntUp"] =loraDeviceInfo.FCntUp;
            reportedProperties["FCntDown"] =loraDeviceInfo.FCntDown;
            await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
            Logger.Log("Join reported properties&fcnt have been set", Logger.LoggingLevel.Info);
           
        }

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


                    //enabling Amqp multiplexing
                    var transportSettings = new ITransportSettings[]
                    {
                        new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                        {
                            AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                            {

                                Pooling = true,
                                MaxPoolSize = 1
                            }
                        }
                    };
                    deviceClient = DeviceClient.CreateFromConnectionString(deviceConnectionStr, transportSettings);
                    //we set the retry only when sending msgs                    
                    deviceClient.SetRetryPolicy(new NoRetry());

                    //if the server disconnects dispose the deviceclient and new one will be created when a new d2c msg comes in.
                    deviceClient.SetConnectionStatusChangesHandler((status, reason) =>
                    {
                        if (status == ConnectionStatus.Disconnected)
                        {
                            deviceClient.Dispose();
                            deviceClient = null;
                            Logger.Log(DevEUI, $"connection closed by the server",Logger.LoggingLevel.Info);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log(DevEUI, $"could not create IoT Hub DeviceClient with error: {ex.Message}", Logger.LoggingLevel.Error);
                }

            }
        }

        public async Task SendMessageAsync(string strMessage,List<KeyValuePair<String,String>> properties)
        {

            if (!string.IsNullOrEmpty(strMessage))
            {

                try
                {
                    CreateDeviceClient();

                    //Enable retry for this send message                 
                    deviceClient.SetRetryPolicy(new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100)));
                    var message = new Message(UTF8Encoding.ASCII.GetBytes(strMessage));
                    foreach (var prop in properties)
                        message.Properties.Add(prop);
                    await deviceClient.SendEventAsync(message);

                    //disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device                    
                    deviceClient.SetRetryPolicy(new NoRetry());
                }
                catch (Exception ex)
                {
                    Logger.Log(DevEUI, $"could not send message to IoTHub/Edge with error: {ex.Message}", Logger.LoggingLevel.Error);
                }

            }
        }


        public async Task<Message> ReceiveAsync(TimeSpan timeout)
        {


            try
            {
                CreateDeviceClient();

                return await deviceClient.ReceiveAsync(timeout);



            }
            catch (Exception ex)
            {
                Logger.Log(DevEUI, $"Could not retrive message to IoTHub/Edge with error: {ex.Message}", Logger.LoggingLevel.Error);
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

            bool enableGateway = true;
            string connectionString = string.Empty;

            string hostName = Environment.GetEnvironmentVariable("IOTEDGE_IOTHUBHOSTNAME");
            string gatewayHostName = Environment.GetEnvironmentVariable("IOTEDGE_GATEWAYHOSTNAME");

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENABLE_GATEWAY")))
                enableGateway = bool.Parse(Environment.GetEnvironmentVariable("ENABLE_GATEWAY"));


            if (string.IsNullOrEmpty(hostName))
            {
                Logger.Log("Environment variable IOTEDGE_IOTHUBHOSTNAME not found, creation of iothub connection not possible", Logger.LoggingLevel.Error);
            }


            connectionString += $"HostName={hostName};";




            if (enableGateway)
            {
                connectionString += $"GatewayHostName={gatewayHostName};";
                Logger.Log(DevEUI, $"using edgeHub local queue", Logger.LoggingLevel.Info);
            }
            else
            {
                Logger.Log(DevEUI, $"{DevEUI} using iotHub directly, no edgeHub queue", Logger.LoggingLevel.Info);
            }



            return connectionString;



        }

        public void Dispose()
        {
            deviceClient.Dispose();
        }
    }
}
