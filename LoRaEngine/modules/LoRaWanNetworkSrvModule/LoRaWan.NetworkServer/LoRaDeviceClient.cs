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
        private static readonly TimeSpan TwinUpdateTimeout = TimeSpan.FromSeconds(10);
        private static int activeDeviceConnections;
        private readonly string connectionString;
        private readonly ITransportSettings[] transportSettings;
        private readonly ILogger<LoRaDeviceClient> logger;
        private readonly Counter<int> twinLoadRequests;
        private DeviceClient deviceClient;

        public LoRaDeviceClient(string connectionString, ITransportSettings[] transportSettings, ILogger<LoRaDeviceClient> logger, Meter meter)
        {
            if (string.IsNullOrEmpty(connectionString)) throw new ArgumentException($"'{nameof(connectionString)}' cannot be null or empty.", nameof(connectionString));
            if (meter is null) throw new ArgumentNullException(nameof(meter));

            this.transportSettings = transportSettings ?? throw new ArgumentNullException(nameof(transportSettings));

            this.connectionString = connectionString;
            this.logger = logger;
            this.twinLoadRequests = meter.CreateCounter<int>(MetricRegistry.TwinLoadRequests);
            _ = meter.CreateObservableGauge(MetricRegistry.ActiveClientConnections, () => activeDeviceConnections);
            this.deviceClient = CreateDeviceClient();
        }

        public async Task<Twin> GetTwinAsync(CancellationToken cancellationToken = default)
        {
            this.twinLoadRequests.Add(1);

            try
            {
                this.logger.LogDebug("getting device twin");

                var twins = await this.deviceClient.GetTwinAsync(cancellationToken);

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
                    cts = new CancellationTokenSource(TwinUpdateTimeout);
                    cancellationToken = cts.Token;
                }

                this.logger.LogDebug("updating twin");

                await this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties, cancellationToken);

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

                    await this.deviceClient.SendEventAsync(message);

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

                var msg = await this.deviceClient.ReceiveAsync(timeout);

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

                await this.deviceClient.CompleteAsync(cloudToDeviceMessage);

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

                await this.deviceClient.AbandonAsync(cloudToDeviceMessage);

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

                await this.deviceClient.RejectAsync(cloudToDeviceMessage);

                this.logger.LogDebug($"done rejecting cloud to device message, id: {cloudToDeviceMessage.MessageId ?? "undefined"}");

                return true;
            }
            catch (OperationCanceledException ex) when (ExceptionFilterUtility.True(() => this.logger.LogError($"could not reject cloud to device message (id: {cloudToDeviceMessage.MessageId ?? "undefined"}) with error: {ex.Message}")))
            {
                return false;
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            if (this.deviceClient != null)
            {
                _ = Interlocked.Decrement(ref activeDeviceConnections);
                await this.deviceClient.CloseAsync(cancellationToken);
#pragma warning disable CA1849 // Calling DisposeAsync after CloseAsync throws an error
                this.deviceClient.Dispose();
#pragma warning restore CA1849 // Call async methods when in an async method
                this.deviceClient = null;

                this.logger.LogDebug("device client disconnected");
            }
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
            _ = Interlocked.Increment(ref activeDeviceConnections);
            var dc = DeviceClient.CreateFromConnectionString(this.connectionString, this.transportSettings);
            dc.SetRetryPolicy(new ExponentialBackoff(int.MaxValue,
                                                     minBackoff: TimeSpan.FromMilliseconds(100),
                                                     maxBackoff: TimeSpan.FromSeconds(10),
                                                     deltaBackoff: TimeSpan.FromMilliseconds(100)));
            return dc;
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync(CancellationToken.None);
        }
    }
}
