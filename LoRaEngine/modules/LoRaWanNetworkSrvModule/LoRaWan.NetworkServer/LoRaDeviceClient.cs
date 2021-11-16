// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Interface between IoT Hub and device.
    /// </summary>
    public sealed class LoRaDeviceClient : ILoRaDeviceClient
    {
        private readonly string devEUI;
        private readonly string connectionString;
        private readonly ITransportSettings[] transportSettings;
        private DeviceClient deviceClient;

        // TODO: verify if those are thread safe and can be static
        private readonly NoRetry noRetryPolicy;
        private readonly ExponentialBackoff exponentialBackoff;

        public LoRaDeviceClient(string devEUI, string connectionString, ITransportSettings[] transportSettings)
        {
            this.devEUI = devEUI;
            this.noRetryPolicy = new NoRetry();
            this.exponentialBackoff = new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));

            this.connectionString = connectionString;
            this.transportSettings = transportSettings;
            this.deviceClient = DeviceClient.CreateFromConnectionString(this.connectionString, this.transportSettings);

            SetRetry(false);
        }

        private void SetRetry(bool retryon)
        {
            if (retryon)
            {
                if (this.deviceClient != null)
                {
                    this.deviceClient.SetRetryPolicy(this.exponentialBackoff);
                }
            }
            else
            {
                if (this.deviceClient != null)
                {
                    this.deviceClient.SetRetryPolicy(this.noRetryPolicy);
                }
            }
        }

        public async Task<Twin> GetTwinAsync()
        {
            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 60000;

                SetRetry(true);

                TcpLogger.Log(this.devEUI, $"getting device twin", LogLevel.Debug);

                var twins = await this.deviceClient.GetTwinAsync();

                TcpLogger.Log(this.devEUI, $"done getting device twin", LogLevel.Debug);

                return twins;
            }
            catch (OperationCanceledException ex)
            {
                TcpLogger.Log(this.devEUI, $"could not retrieve device twin with error: {ex.Message}", LogLevel.Error);
                return null;
            }
            catch (IotHubCommunicationException ex)
            {
                throw new LoRaProcessingException("Error when communicating with IoT Hub while fetching the device twin.", ex, LoRaProcessingErrorCode.TwinFetchFailed);
            }
            catch (IotHubException ex)
            {
                throw new LoRaProcessingException("An error occured in IoT Hub when fetching the device twin.", ex, LoRaProcessingErrorCode.TwinFetchFailed);
            }
            finally
            {
                SetRetry(false);
            }
        }

        public async Task<bool> UpdateReportedPropertiesAsync(TwinCollection reportedProperties)
        {
            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 120000;

                SetRetry(true);

                TcpLogger.Log(this.devEUI, $"updating twin", LogLevel.Debug);

                await this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

                TcpLogger.Log(this.devEUI, $"twin updated", LogLevel.Debug);

                return true;
            }
            catch (OperationCanceledException ex)
            {
                TcpLogger.Log(this.devEUI, $"could not update twin with error: {ex.Message}", LogLevel.Error);
                return false;
            }
            finally
            {
                SetRetry(false);
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
                    SetRetry(true);

                    var messageJson = JsonConvert.SerializeObject(telemetry, Formatting.None);
                    using var message = new Message(Encoding.UTF8.GetBytes(messageJson));

                    TcpLogger.Log(this.devEUI, $"sending message {messageJson} to hub", LogLevel.Debug);

                    message.ContentType = System.Net.Mime.MediaTypeNames.Application.Json;
                    message.ContentEncoding = Encoding.UTF8.BodyName;

                    if (properties != null)
                    {
                        foreach (var prop in properties)
                            message.Properties.Add(prop);
                    }

                    await this.deviceClient.SendEventAsync(message);

                    return true;
                }
                catch (OperationCanceledException ex)
                {
                    TcpLogger.Log(this.devEUI, $"could not send message to IoTHub/Edge with error: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                    SetRetry(false);
                }
            }

            return false;
        }

        public async Task<Message> ReceiveAsync(TimeSpan timeout)
        {
            try
            {
                // Set the operation timeout to accepted timeout plus one second
                // Should not return an operation timeout since we wait less that it
                this.deviceClient.OperationTimeoutInMilliseconds = (uint)(timeout.TotalMilliseconds + 1000);

                SetRetry(true);

                TcpLogger.Log(this.devEUI, $"checking cloud to device message for {timeout}", LogLevel.Debug);

                var msg = await this.deviceClient.ReceiveAsync(timeout);

                if (TcpLogger.LoggerLevel >= LogLevel.Debug)
                {
                    if (msg == null)
                        TcpLogger.Log(this.devEUI, "done checking cloud to device message, found no message", LogLevel.Debug);
                    else
                        TcpLogger.Log(this.devEUI, $"done checking cloud to device message, found message id: {msg.MessageId ?? "undefined"}", LogLevel.Debug);
                }

                return msg;
            }
            catch (OperationCanceledException ex)
            {
                TcpLogger.Log(this.devEUI, $"could not retrieve cloud to device message with error: {ex.Message}", LogLevel.Error);
                return null;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                SetRetry(false);
            }
        }

        public async Task<bool> CompleteAsync(Message cloudToDeviceMessage)
        {
            if (cloudToDeviceMessage is null) throw new ArgumentNullException(nameof(cloudToDeviceMessage));

            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 30000;

                SetRetry(true);

                TcpLogger.Log(this.devEUI, $"completing cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Debug);

                await this.deviceClient.CompleteAsync(cloudToDeviceMessage);

                TcpLogger.Log(this.devEUI, $"done completing cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Debug);

                return true;
            }
            catch (OperationCanceledException ex)
            {
                TcpLogger.Log(this.devEUI, $"could not complete cloud to device message (id: {cloudToDeviceMessage.MessageId ?? "undefined"}) with error: {ex.Message}", LogLevel.Error);
                return false;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                SetRetry(false);
            }
        }

        public async Task<bool> AbandonAsync(Message cloudToDeviceMessage)
        {
            if (cloudToDeviceMessage is null) throw new ArgumentNullException(nameof(cloudToDeviceMessage));

            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 30000;

                SetRetry(true);

                TcpLogger.Log(this.devEUI, $"abandoning cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Debug);

                await this.deviceClient.AbandonAsync(cloudToDeviceMessage);

                TcpLogger.Log(this.devEUI, $"done abandoning cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Debug);

                return true;
            }
            catch (OperationCanceledException ex)
            {
                TcpLogger.Log(this.devEUI, $"could not abandon cloud to device message (id: {cloudToDeviceMessage.MessageId ?? "undefined"}) with error: {ex.Message}", LogLevel.Error);
                return false;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                SetRetry(false);
            }
        }

        public async Task<bool> RejectAsync(Message cloudToDeviceMessage)
        {
            if (cloudToDeviceMessage is null) throw new ArgumentNullException(nameof(cloudToDeviceMessage));

            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 30000;

                SetRetry(true);

                TcpLogger.Log(this.devEUI, $"rejecting cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Debug);

                await this.deviceClient.RejectAsync(cloudToDeviceMessage);

                TcpLogger.Log(this.devEUI, $"done rejecting cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}", LogLevel.Debug);

                return true;
            }
            catch (OperationCanceledException ex)
            {
                TcpLogger.Log(this.devEUI, $"could not reject cloud to device message (id: {cloudToDeviceMessage.MessageId ?? "undefined"}) with error: {ex.Message}", LogLevel.Error);
                return false;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                SetRetry(false);
            }
        }

        /// <summary>
        /// Disconnects device client.
        /// </summary>
        public bool Disconnect()
        {
            if (this.deviceClient != null)
            {
                this.deviceClient.Dispose();
                this.deviceClient = null;

                TcpLogger.Log(this.devEUI, "device client disconnected", LogLevel.Debug);
            }
            else
            {
                TcpLogger.Log(this.devEUI, "device client was already disconnected", LogLevel.Debug);
            }

            return true;
        }

        /// <summary>
        /// Ensures that the connection is open.
        /// </summary>
        public bool EnsureConnected()
        {
            if (this.deviceClient == null)
            {
                try
                {
                    this.deviceClient = DeviceClient.CreateFromConnectionString(this.connectionString, this.transportSettings);
                    TcpLogger.Log(this.devEUI, "device client reconnected", LogLevel.Debug);
                }
                catch (ArgumentException ex)
                {
                    TcpLogger.Log(this.devEUI, $"could not connect device client with error: {ex.Message}", LogLevel.Error);
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            this.deviceClient?.Dispose();
            this.deviceClient = null;

            GC.SuppressFinalize(this);
        }
    }
}
