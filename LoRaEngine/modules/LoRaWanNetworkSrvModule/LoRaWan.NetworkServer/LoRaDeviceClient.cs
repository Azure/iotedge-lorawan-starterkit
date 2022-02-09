// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
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
        private static readonly TimeSpan twinUpdateTimeout = TimeSpan.FromSeconds(10);
        private readonly string connectionString;
        private readonly ITransportSettings[] transportSettings;
        private readonly ILogger<LoRaDeviceClient> logger;
        private readonly ModuleConnectionHost moduleConnectionHost;
        private readonly string deviceId;
        private readonly Counter<int> twinLoadRequests;
        private DeviceClient deviceClient;

        private readonly string primaryKey;

        public LoRaDeviceClient(string connectionString, ITransportSettings[] transportSettings, string primaryKey, ILogger<LoRaDeviceClient> logger, Meter meter, ModuleConnectionHost moduleConnectionHost, string deviceId)
        {
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or empty.", nameof(connectionString));
            if (string.IsNullOrEmpty(primaryKey)) throw new ArgumentException($"'{nameof(primaryKey)}' cannot be null or empty.", nameof(primaryKey));
            if (meter is null) throw new ArgumentNullException(nameof(meter));

            this.transportSettings = transportSettings ?? throw new ArgumentNullException(nameof(transportSettings));

            this.connectionString = connectionString;
            this.primaryKey = primaryKey;
            this.logger = logger;
            this.moduleConnectionHost = moduleConnectionHost;
            this.deviceId = deviceId;
            this.twinLoadRequests = meter.CreateCounter<int>(MetricRegistry.TwinLoadRequests);
            this.deviceClient = CreateDeviceClient();
        }

        public bool IsMatchingKey(string primaryKey) => this.primaryKey == primaryKey;

        public async Task<Twin> GetTwinAsync(CancellationToken cancellationToken = default)
        {
            this.twinLoadRequests.Add(1);

            try
            {
                this.logger.LogDebug("getting device twin");

                await this.deviceClient.OpenAsync(cancellationToken);
                var twins = await this.deviceClient.GetTwinAsync(cancellationToken);
                await this.deviceClient.CloseAsync(cancellationToken);

                this.logger.LogDebug("done getting device twin");

                return twins;
            }
            catch (OperationCanceledException ex)
            {
                this.logger.LogError($"could not retrieve device twin with error: {ex.Message}");
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
        }

        public async Task<bool> UpdateReportedPropertiesAsync(TwinCollection reportedProperties, CancellationToken cancellationToken)
        {
            CancellationTokenSource cts = default;
            try
            {
                if (cancellationToken == default)
                {
                    cts = new CancellationTokenSource(twinUpdateTimeout);
                    cancellationToken = cts.Token;
                }

                this.logger.LogDebug("updating twin");

                await this.deviceClient.OpenAsync(cancellationToken);
                await this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken);
                await this.deviceClient.CloseAsync(cancellationToken);

                this.logger.LogDebug("twin updated");

                return true;
            }
            catch (IotHubCommunicationException ex) when (ex.InnerException is OperationCanceledException &&
                                                          ExceptionFilterUtility.True(() => this.logger.LogError($"could not update twin with error: {ex.Message}")))
            {
                return false;
            }
            finally
            {
                cts?.Dispose();
            }
        }

        public async Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties)
        {
            if (telemetry != null)
            {
                try
                {
                    var messageJson = JsonConvert.SerializeObject(telemetry, Formatting.None);
                    using var message = new Message(Encoding.UTF8.GetBytes(messageJson));

                    this.logger.LogDebug($"sending message {messageJson} to hub");

                    message.ContentType = System.Net.Mime.MediaTypeNames.Application.Json;
                    message.ContentEncoding = Encoding.UTF8.BodyName;

                    if (properties != null)
                    {
                        foreach (var prop in properties)
                            message.Properties.Add(prop);
                    }

                    await this.moduleConnectionHost.SendEventAsync(deviceId, message, default);

                    return true;
                }
                catch (OperationCanceledException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError($"could not send message to IoTHub/Edge with error: {ex.Message}")))
                {
                    // continue
                }
            }

            return false;
        }

        public async Task<Message> ReceiveAsync(TimeSpan timeout)
        {
            try
            {
                this.logger.LogDebug($"checking cloud to device message for {timeout}");

                await this.deviceClient.OpenAsync();
                var msg = await this.deviceClient.ReceiveAsync(timeout);
                await this.deviceClient.CloseAsync();

                if (this.logger.IsEnabled(LogLevel.Debug))
                {
                    if (msg == null)
                        this.logger.LogDebug("done checking cloud to device message, found no message");
                    else
                        this.logger.LogDebug($"done checking cloud to device message, found message id: {msg.MessageId ?? "undefined"}");
                }

                return msg;
            }
            catch (OperationCanceledException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError($"could not retrieve cloud to device message with error: {ex.Message}")))
            {
                return null;
            }
        }

        public async Task<bool> CompleteAsync(Message cloudToDeviceMessage)
        {
            if (cloudToDeviceMessage is null) throw new ArgumentNullException(nameof(cloudToDeviceMessage));

            try
            {
                this.logger.LogDebug($"completing cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}");

                await this.deviceClient.OpenAsync();
                await this.deviceClient.CompleteAsync(cloudToDeviceMessage);
                await this.deviceClient.CloseAsync();

                this.logger.LogDebug($"done completing cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}");

                return true;
            }
            catch (OperationCanceledException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError($"could not complete cloud to device message (id: {cloudToDeviceMessage.MessageId ?? "undefined"}) with error: {ex.Message}")))
            {
                return false;
            }
        }

        public async Task<bool> AbandonAsync(Message cloudToDeviceMessage)
        {
            if (cloudToDeviceMessage is null) throw new ArgumentNullException(nameof(cloudToDeviceMessage));

            try
            {
                this.logger.LogDebug($"abandoning cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}");

                await this.deviceClient.OpenAsync();
                await this.deviceClient.AbandonAsync(cloudToDeviceMessage);
                await this.deviceClient.CloseAsync();

                this.logger.LogDebug($"done abandoning cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}");

                return true;
            }
            catch (OperationCanceledException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError($"could not abandon cloud to device message (id: {cloudToDeviceMessage.MessageId ?? "undefined"}) with error: {ex.Message}")))
            {
                return false;
            }
        }

        public async Task<bool> RejectAsync(Message cloudToDeviceMessage)
        {
            if (cloudToDeviceMessage is null) throw new ArgumentNullException(nameof(cloudToDeviceMessage));

            try
            {
                this.logger.LogDebug($"rejecting cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}");

                await this.deviceClient.OpenAsync();
                await this.deviceClient.RejectAsync(cloudToDeviceMessage);
                await this.deviceClient.CloseAsync();

                this.logger.LogDebug($"done rejecting cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}");

                return true;
            }
            catch (OperationCanceledException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError($"could not reject cloud to device message (id: {cloudToDeviceMessage.MessageId ?? "undefined"}) with error: {ex.Message}")))
            {
                return false;
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

                this.logger.LogDebug("device client disconnected");
            }
            else
            {
                this.logger.LogDebug("device client was already disconnected");
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
                    this.deviceClient = CreateDeviceClient();
                    this.logger.LogDebug("device client reconnected");
                }
                catch (ArgumentException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError($"could not connect device client with error: {ex.Message}")))
                {
                    return false;
                }
            }

            return true;
        }

        private DeviceClient CreateDeviceClient()
        {
            var dc = DeviceClient.CreateFromConnectionString(this.connectionString, this.transportSettings);
            dc.SetRetryPolicy(new ExponentialBackoff(int.MaxValue,
                                                     minBackoff: TimeSpan.FromMilliseconds(100),
                                                     maxBackoff: TimeSpan.FromSeconds(10),
                                                     deltaBackoff: TimeSpan.FromMilliseconds(100)));
            return dc;
        }

        public void Dispose()
        {
            this.deviceClient?.Dispose();
            this.deviceClient = null;

            GC.SuppressFinalize(this);
        }
    }
}
