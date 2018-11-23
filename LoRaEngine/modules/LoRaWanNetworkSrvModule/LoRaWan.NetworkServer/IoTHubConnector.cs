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
    public class IoTHubConnector : IDisposable
    {
        private DeviceClient deviceClient;

        private string DevEUI;

        private string PrimaryKey;
        private readonly NetworkServerConfiguration configuration;

        public async Task<Twin> GetTwinAsync()
        {
            try
            {
              
                CreateDeviceClient();

                deviceClient.OperationTimeoutInMilliseconds = 5000;

                setRetry(true);

                Logger.Log(DevEUI, $"getting twins", Logger.LoggingLevel.Full);

                var twin = await deviceClient.GetTwinAsync();             

                Logger.Log(DevEUI, $"done getting twins", Logger.LoggingLevel.Full);

                setRetry(false);

                return twin;
            }
            catch (Exception ex)
            {
                Logger.Log(DevEUI, $"could not get twins with error: {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }

        }

        public async Task UpdateFcntAsync(int FCntUp, int? FCntDown, bool force = false)
        {
            try
            {

                //update the twins every 10
                if (FCntUp % 10 == 0 || force == true )
                {
                  

                    CreateDeviceClient();

                    deviceClient.OperationTimeoutInMilliseconds = 10000;

                    setRetry(true);

                    TwinCollection reportedProperties = new TwinCollection();

                    if (FCntDown != null)
                    {
                        reportedProperties["FCntDown"] = FCntDown;                      
                    }                   
                    reportedProperties["FCntUp"] = FCntUp;

                    Logger.Log(DevEUI, $"updating twins {FCntUp}:{FCntDown}", Logger.LoggingLevel.Full);

                    await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

                    Logger.Log(DevEUI, $"twins updated {FCntUp}:{FCntDown}", Logger.LoggingLevel.Full);

                    setRetry(false);

                    
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
        public async Task<bool> UpdateReportedPropertiesOTAAasync(LoraDeviceInfo loraDeviceInfo)
        {
            try
            {
                CreateDeviceClient();

                deviceClient.OperationTimeoutInMilliseconds = 4000;

                setRetry(true);

                Logger.Log(DevEUI, $"saving join properties twins", Logger.LoggingLevel.Full);

                TwinCollection reportedProperties = new TwinCollection();
                reportedProperties["NwkSKey"] = loraDeviceInfo.NwkSKey;
                reportedProperties["AppSKey"] = loraDeviceInfo.AppSKey;
                reportedProperties["DevEUI"] = loraDeviceInfo.DevEUI;
                reportedProperties["DevAddr"] = loraDeviceInfo.DevAddr;
                reportedProperties["NetId"] = loraDeviceInfo.NetId;
                reportedProperties["FCntUp"] = loraDeviceInfo.FCntUp;
                reportedProperties["FCntDown"] = loraDeviceInfo.FCntDown;
                reportedProperties["DevNonce"] = loraDeviceInfo.DevNonce;
                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

                Logger.Log(DevEUI, $"done saving join properties twins", Logger.LoggingLevel.Full);

                setRetry(false);

                return true;

            }
            catch (Exception ex)
            {
                Logger.Log(DevEUI, $"could not save twins with error: {ex.Message}", Logger.LoggingLevel.Error);
                return false;
            }
        }

        public IoTHubConnector(string DevEUI, string PrimaryKey, NetworkServerConfiguration configuration)
        {
            this.DevEUI = DevEUI;
            this.PrimaryKey = PrimaryKey;
            this.configuration = configuration;
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
                    setRetry(false);

                    //if the server disconnects dispose the deviceclient and new one will be created when a new d2c msg comes in.
                    deviceClient.SetConnectionStatusChangesHandler((status, reason) =>
                    {
                        if (status == ConnectionStatus.Disconnected)
                        {
                            //if (deviceClient != null)
                            //{
                            //    deviceClient.Dispose();
                            //    deviceClient = null;
                            //}
                            //todo ronnie should we log the closing of the connection?
                            //Logger.Log(DevEUI, $"connection closed by the server",Logger.LoggingLevel.Info);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log(DevEUI, $"could not create IoT Hub DeviceClient with error: {ex.Message}", Logger.LoggingLevel.Error);
                }

            }
        }

        private void setRetry(bool retryon)
        {
            if (retryon)
            {
                if (deviceClient != null)
                {
                    deviceClient.SetRetryPolicy(new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100)));
                    //Logger.Log(DevEUI, $"retry is on", Logger.LoggingLevel.Full);
                }
            }
            else
            {
                if (deviceClient != null)
                {
                    deviceClient.SetRetryPolicy(new NoRetry());
                    //Logger.Log(DevEUI, $"retry is off", Logger.LoggingLevel.Full);
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

                    deviceClient.OperationTimeoutInMilliseconds = 10000;

                    //Enable retry for this send message, off by default              
                    setRetry(true);

                    var message = new Message(UTF8Encoding.ASCII.GetBytes(strMessage));

                    Logger.Log(DevEUI, $"sending message {strMessage} to hub", Logger.LoggingLevel.Full);

                    foreach (var prop in properties)
                        message.Properties.Add(prop);
                    await deviceClient.SendEventAsync(message);

                    Logger.Log(DevEUI, $"sent message {strMessage} to hub", Logger.LoggingLevel.Full);

                    //disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device                    
                    setRetry(false);
                }
                catch (Exception ex)
                {
                    //disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device                    
                     setRetry(false);

                    Logger.Log(DevEUI, $"could not send message to IoTHub/Edge with error: {ex.Message}", Logger.LoggingLevel.Error);
                   
                }

            }
        }

        public async Task<Message> ReceiveAsync(TimeSpan timeout)
        {
            try
            {
                CreateDeviceClient();

                deviceClient.OperationTimeoutInMilliseconds = 1500;

                setRetry(true);

                Logger.Log(DevEUI, $"checking c2d message", Logger.LoggingLevel.Full);

                Message msg = await deviceClient.ReceiveAsync(timeout);

                Logger.Log(DevEUI, $"done checking c2d message", Logger.LoggingLevel.Full);

                setRetry(false);

                return msg;

            }
            catch (Exception ex)
            {
                Logger.Log(DevEUI, $"Could not retrieve c2d message with error: {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }
        }

        public async Task<bool> CompleteAsync(Message message)
        {
            try
            {
                CreateDeviceClient();

                deviceClient.OperationTimeoutInMilliseconds = 1500;

                setRetry(true);

                Logger.Log(DevEUI, $"completing c2d message", Logger.LoggingLevel.Full);

                await deviceClient.CompleteAsync(message);

                Logger.Log(DevEUI, $"done completing c2d message", Logger.LoggingLevel.Full);

                setRetry(false);

                return true;

               
            }
            catch (Exception ex)
            {
                Logger.Log(DevEUI, $"could not complete c2d with error: {ex.Message}", Logger.LoggingLevel.Error);
                return false;
            }
          
        }

        public async Task<bool> AbandonAsync(Message message)
        {

            try
            {
                CreateDeviceClient();

                deviceClient.OperationTimeoutInMilliseconds = 1500;

                setRetry(true);

                Logger.Log(DevEUI, $"abbandoning c2d message", Logger.LoggingLevel.Full);

                await deviceClient.AbandonAsync(message);

                Logger.Log(DevEUI, $"done abbandoning c2d message", Logger.LoggingLevel.Full);

                setRetry(false);

                return true;


            }
            catch (Exception ex)
            {
                Logger.Log(DevEUI, $"could not abbandon c2d with error: {ex.Message}", Logger.LoggingLevel.Error);
                return false;

            }
                   
        }

        private string createIoTHubConnectionString()
        {
            string connectionString = string.Empty;


            if (string.IsNullOrEmpty(configuration.IoTHubHostName))
            {
                Logger.Log("Configuration/Environment variable IOTEDGE_IOTHUBHOSTNAME not found, creation of iothub connection not possible", Logger.LoggingLevel.Error);
            }

            connectionString += $"HostName={configuration.IoTHubHostName};";

            if (configuration.EnableGateway)
            {
                connectionString += $"GatewayHostName={configuration.GatewayHostName};";
                Logger.Log(DevEUI, $"using edgeHub local queue", Logger.LoggingLevel.Info);
            }
            else
            {
                Logger.Log(DevEUI, $"using iotHub directly, no edgeHub queue", Logger.LoggingLevel.Info);
            }
            return connectionString;
        }

        public void Dispose()
        {
            deviceClient.Dispose();
        }
    }
}
