//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//


using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace LoRaWan.NetworkServer.V2
{

    public sealed class LoRaDeviceClient : ILoRaDeviceClient
    {
        private readonly string devEUI;
        private DeviceClient deviceClient;

        public LoRaDeviceClient(string devEUI, DeviceClient deviceClient)
        {
            this.devEUI = devEUI;
            this.deviceClient = deviceClient;
            SetRetry(false);
        }


        private void SetRetry(bool retryon)
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

        public async Task<Twin> GetTwinAsync()
        {
            try
            {
                deviceClient.OperationTimeoutInMilliseconds = 1500;

                SetRetry(true);

                Logger.Log(this.devEUI, $"getting device twins", Logger.LoggingLevel.Full);

                var twins = await this.deviceClient.GetTwinAsync();

                Logger.Log(this.devEUI, $"done getting device twins", Logger.LoggingLevel.Full);

                SetRetry(false);

                return twins;

            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"Could not retrieve device twins with error: {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }
            
        }


        public async Task UpdateReportedPropertiesAsync(Dictionary<string, object> values)
        {
            try
            {

                deviceClient.OperationTimeoutInMilliseconds = 120000;

                SetRetry(true);

                var logMessage = new StringBuilder();
                var reportedProperties = new TwinCollection();
                foreach (var kv in values)
                {
                    if (logMessage.Length > 0)
                        logMessage.Append(',');
                    logMessage.Append(kv.Key).Append(':').Append(kv.Value ?? "null");
                    reportedProperties[kv.Key] = kv.Value;
                }

                var finalLogMessage = logMessage.ToString();
                Logger.Log(this.devEUI, $"updating twins {finalLogMessage}", Logger.LoggingLevel.Full);

                await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

                Logger.Log(this.devEUI, $"twins updated {finalLogMessage}", Logger.LoggingLevel.Full);

                SetRetry(false);

            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not update twins with error: {ex.Message}", Logger.LoggingLevel.Error);
            }


        }

        public async Task SendEventAsync(string messageBody, Dictionary<string, string> properties)
        {

            if (!string.IsNullOrEmpty(messageBody))
            {
                try
                {
                    deviceClient.OperationTimeoutInMilliseconds = 120000;

                    //Enable retry for this send message, off by default              
                    SetRetry(true);

                    var message = new Message(Encoding.UTF8.GetBytes(messageBody));

                    Logger.Log(this.devEUI, $"sending message {messageBody} to hub", Logger.LoggingLevel.Full);

                    message.ContentType = System.Net.Mime.MediaTypeNames.Application.Json;
                    message.ContentEncoding = Encoding.UTF8.BodyName;

                    if (properties != null)
                    {
                        foreach (var prop in properties)
                            message.Properties.Add(prop);
                    }
                    
                    await deviceClient.SendEventAsync(message);

                    Logger.Log(this.devEUI, $"sent message {messageBody} to hub", Logger.LoggingLevel.Full);

                    //disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device                    
                    SetRetry(false);
                }
                catch (Exception ex)
                {
                    //disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device                    
                     SetRetry(false);

                    Logger.Log(this.devEUI, $"could not send message to IoTHub/Edge with error: {ex.Message}", Logger.LoggingLevel.Error);
                   
                }

            }
        }

        public async Task<Message> ReceiveAsync(TimeSpan timeout)
        {
            try
            {
                deviceClient.OperationTimeoutInMilliseconds = 1500;

                SetRetry(true);

                Logger.Log(this.devEUI, $"checking c2d message", Logger.LoggingLevel.Full);

                Message msg = await deviceClient.ReceiveAsync(timeout);

                Logger.Log(this.devEUI, $"done checking c2d message", Logger.LoggingLevel.Full);

                SetRetry(false);

                return msg;

            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"Could not retrieve c2d message with error: {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }
        }

        public async Task<bool> CompleteAsync(Message message)
        {
            try
            {
                deviceClient.OperationTimeoutInMilliseconds = 30000;

                SetRetry(true);

                Logger.Log(this.devEUI, $"completing c2d message", Logger.LoggingLevel.Full);

                await deviceClient.CompleteAsync(message);

                Logger.Log(this.devEUI, $"done completing c2d message", Logger.LoggingLevel.Full);

                SetRetry(false);

                return true;

               
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not complete c2d with error: {ex.Message}", Logger.LoggingLevel.Error);
                return false;
            }
          
        }

        public async Task<bool> AbandonAsync(Message message)
        {

            try
            {
                deviceClient.OperationTimeoutInMilliseconds = 30000;

                SetRetry(true);

                Logger.Log(this.devEUI, $"abandoning c2d message", Logger.LoggingLevel.Full);

                await deviceClient.AbandonAsync(message);

                Logger.Log(this.devEUI, $"done abandoning c2d message", Logger.LoggingLevel.Full);

                SetRetry(false);

                return true;


            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not abandon c2d with error: {ex.Message}", Logger.LoggingLevel.Error);
                return false;

            }
                   
        }

        public void Dispose()
        {
            deviceClient.Dispose();
        }
    }
}