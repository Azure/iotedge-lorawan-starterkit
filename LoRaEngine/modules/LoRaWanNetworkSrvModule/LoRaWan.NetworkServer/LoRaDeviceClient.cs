// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Newtonsoft.Json;

    /// <summary>
    /// Interface between IoT Hub and device
    /// </summary>
    public sealed class LoRaDeviceClient : ILoRaDeviceClient
    {
        // Maximum time waited to receive a message async
        const int MaxReceiveMessageTimeoutInMs = 400;

        private readonly string devEUI;
        private DeviceClient deviceClient;

        // TODO: verify if those are thread safe and can be static
        NoRetry noRetryPolicy;
        ExponentialBackoff exponentialBackoff;

        public LoRaDeviceClient(string devEUI, DeviceClient deviceClient)
        {
            this.devEUI = devEUI;
            this.deviceClient = deviceClient;

            this.noRetryPolicy = new NoRetry();
            this.exponentialBackoff = new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));

            this.SetRetry(false);
        }

        private void SetRetry(bool retryon)
        {
            if (retryon)
            {
                if (this.deviceClient != null)
                {
                    this.deviceClient.SetRetryPolicy(this.exponentialBackoff);
                    // Logger.Log(DevEUI, $"retry is on", Logger.LoggingLevel.Full);
                }
            }
            else
            {
                if (this.deviceClient != null)
                {
                    this.deviceClient.SetRetryPolicy(this.noRetryPolicy);
                    // Logger.Log(DevEUI, $"retry is off", Logger.LoggingLevel.Full);
                }
            }
        }

        public async Task<Twin> GetTwinAsync()
        {
            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 60000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"getting device twins", Logger.LoggingLevel.Full);

                var twins = await this.deviceClient.GetTwinAsync();

                Logger.Log(this.devEUI, $"done getting device twins", Logger.LoggingLevel.Full);

                return twins;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"Could not retrieve device twins with error: {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }
            finally
            {
                this.SetRetry(false);
            }
        }

        public async Task<bool> UpdateReportedPropertiesAsync(TwinCollection reportedProperties)
        {
            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 120000;

                this.SetRetry(true);

                string reportedPropertiesJson = string.Empty;
                if (Logger.LoggerLevel == Logger.LoggingLevel.Full)
                {
                    reportedPropertiesJson = reportedProperties.ToJson(Newtonsoft.Json.Formatting.None);
                    Logger.Log(this.devEUI, $"updating twins {reportedPropertiesJson}", Logger.LoggingLevel.Full);
                }

                await this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

                if (Logger.LoggerLevel == Logger.LoggingLevel.Full)
                    Logger.Log(this.devEUI, $"twins updated {reportedPropertiesJson}", Logger.LoggingLevel.Full);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not update twins with error: {ex.Message}", Logger.LoggingLevel.Error);
                return false;
            }
            finally
            {
                this.SetRetry(false);
            }
        }

        public async Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties)
        {
            if (telemetry != null)
            {
                try
                {
                    this.deviceClient.OperationTimeoutInMilliseconds = 120000;

                    // Enable retry for this send message, off by default
                    this.SetRetry(true);

                    var messageJson = JsonConvert.SerializeObject(telemetry, Formatting.None);
                    var message = new Message(Encoding.UTF8.GetBytes(messageJson));

                    Logger.Log(this.devEUI, $"sending message {messageJson} to hub", Logger.LoggingLevel.Full);

                    message.ContentType = System.Net.Mime.MediaTypeNames.Application.Json;
                    message.ContentEncoding = Encoding.UTF8.BodyName;

                    if (properties != null)
                    {
                        foreach (var prop in properties)
                            message.Properties.Add(prop);
                    }

                    await this.deviceClient.SendEventAsync(message);

                    Logger.Log(this.devEUI, $"sent message {messageJson} to hub", Logger.LoggingLevel.Full);

                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Log(this.devEUI, $"could not send message to IoTHub/Edge with error: {ex.Message}", Logger.LoggingLevel.Error);
                }
                finally
                {
                    // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                    this.SetRetry(false);
                }
            }

            return false;
        }

        public async Task<Message> ReceiveAsync(TimeSpan timeout)
        {
            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 1500;

                this.SetRetry(true);

                if (timeout.TotalMilliseconds > MaxReceiveMessageTimeoutInMs)
                    timeout = TimeSpan.FromMilliseconds(MaxReceiveMessageTimeoutInMs);

                Logger.Log(this.devEUI, $"checking c2d message for {timeout}", Logger.LoggingLevel.Full);

                Message msg = await this.deviceClient.ReceiveAsync(timeout);

                if (Logger.LoggerLevel >= Logger.LoggingLevel.Full)
                {
                    if (msg == null)
                        Logger.Log(this.devEUI, "done checking c2d message, found no message", Logger.LoggingLevel.Full);
                    else
                        Logger.Log(this.devEUI, $"done checking c2d message, found message id: {msg.MessageId ?? "undefined"}", Logger.LoggingLevel.Full);
                }

                return msg;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"Could not retrieve c2d message with error: {ex.Message}", Logger.LoggingLevel.Error);
                return null;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                this.SetRetry(false);
            }
        }

        public async Task<bool> CompleteAsync(Message message)
        {
            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 30000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"completing c2d message, id: {message.MessageId ?? "undefined"}", Logger.LoggingLevel.Full);

                await this.deviceClient.CompleteAsync(message);

                Logger.Log(this.devEUI, $"done completing c2d message, id: {message.MessageId ?? "undefined"}", Logger.LoggingLevel.Full);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not complete c2d message (id: {message.MessageId ?? "undefined"}) with error: {ex.Message}", Logger.LoggingLevel.Error);
                return false;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                this.SetRetry(false);
            }
        }

        public async Task<bool> AbandonAsync(Message message)
        {
            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 30000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"abandoning c2d message, id: {message.MessageId ?? "undefined"}", Logger.LoggingLevel.Full);

                await this.deviceClient.AbandonAsync(message);

                Logger.Log(this.devEUI, $"done abandoning c2d message, id: {message.MessageId ?? "undefined"}", Logger.LoggingLevel.Full);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not abandon c2d message (id: {message.MessageId ?? "undefined"}) with error: {ex.Message}", Logger.LoggingLevel.Error);
                return false;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                this.SetRetry(false);
            }
        }

        public void Dispose()
        {
            this.deviceClient.Dispose();
        }
    }
}