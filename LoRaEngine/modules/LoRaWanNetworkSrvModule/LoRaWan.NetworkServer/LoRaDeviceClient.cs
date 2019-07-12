// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    /// <summary>
    /// Interface between IoT Hub and device
    /// </summary>
    public sealed class LoRaDeviceClient : ILoRaDeviceClient
    {
        private readonly string devEUI;
        private readonly string connectionString;
        private readonly ILoRaDeviceFactory deviceFactory;
        private readonly NoRetry noRetryPolicy;
        private readonly ExponentialBackoff exponentialBackoff;
        private SemaphoreSlim receiveAsyncLock;
        private TaskCompletionSource<Message> receiveAsyncTaskCompletionSource;
        private int receiveAsyncTaskWaitCount;
        private IIoTHubDeviceClient deviceClient;

        public LoRaDeviceClient(string devEUI, string connectionString, ILoRaDeviceFactory deviceFactory)
        {
            this.devEUI = devEUI;
            this.noRetryPolicy = new NoRetry();
            this.exponentialBackoff = new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));

            this.connectionString = connectionString;
            this.deviceFactory = deviceFactory;
            this.deviceClient = deviceFactory.CreateDeviceClient(this.connectionString);

            this.SetRetry(false);

            this.receiveAsyncLock = new SemaphoreSlim(1, 1);
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
                this.deviceClient.OperationTimeoutInMilliseconds = 120_000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"getting device twin", LogLevel.Debug);

                var twins = await this.deviceClient.GetTwinAsync();

                Logger.Log(this.devEUI, $"done getting device twin", LogLevel.Debug);

                return twins;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not retrieve device twin with error: {ex.Message}", LogLevel.Error);
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
                this.deviceClient.OperationTimeoutInMilliseconds = 120_000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"updating twin", LogLevel.Debug);

                await this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);

                Logger.Log(this.devEUI, $"twin updated", LogLevel.Debug);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not update twin with error: {ex.Message}", LogLevel.Error);
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
                    this.deviceClient.OperationTimeoutInMilliseconds = 120_000;

                    // Enable retry for this send message, off by default
                    this.SetRetry(true);

                    var messageJson = JsonConvert.SerializeObject(telemetry, Formatting.None);
                    var message = new Message(Encoding.UTF8.GetBytes(messageJson));

                    Logger.Log(this.devEUI, $"sending message {messageJson} to hub", LogLevel.Debug);

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
                catch (Exception ex)
                {
                    Logger.Log(this.devEUI, $"could not send message to IoTHub/Edge with error: {ex.Message}", LogLevel.Error);
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
            var isUsingPendingRequest = true;

            TaskCompletionSource<Message> localPendingReceiveAsync = null;

            await this.receiveAsyncLock.WaitAsync();

            try
            {
                if ((localPendingReceiveAsync = this.receiveAsyncTaskCompletionSource) == null)
                {
                    localPendingReceiveAsync = this.SetupNewReceiveAsyncTaskCompletionSource(timeout);
                    isUsingPendingRequest = false;
                }
            }
            finally
            {
                this.receiveAsyncTaskWaitCount++;
                this.receiveAsyncLock.Release();
            }

            if (isUsingPendingRequest)
            {
                Logger.Log(this.devEUI, $"checking cloud to device message for {timeout}, reusing pending request", LogLevel.Debug);
            }

            using (var cts = new CancellationTokenSource())
            {
                var timer = Task.Delay(timeout, cts.Token);
                var winner = await Task.WhenAny(localPendingReceiveAsync.Task, timer);
                if (winner == localPendingReceiveAsync.Task)
                {
                    // Cancel the timer tasks so that it does not fire
                    cts.Cancel();

                    Task<Message> singleFinished;
                    await this.receiveAsyncLock.WaitAsync();
                    try
                    {
                        if (localPendingReceiveAsync == this.receiveAsyncTaskCompletionSource)
                        {
                            // Verbose log as long as this is a new feature
                            Logger.Log(this.devEUI, $"task ReceiveAsync returned before timeout", LogLevel.Debug);
                            singleFinished = this.receiveAsyncTaskCompletionSource.Task;

                            this.receiveAsyncTaskCompletionSource = null;
                            this.receiveAsyncTaskWaitCount = 0;
                        }
                        else
                        {
                            singleFinished = null;
                        }
                    }
                    finally
                    {
                        this.receiveAsyncLock.Release();
                    }

                    // Finished can be null if two race for the value of pendingReceiveAsync
                    // In that case the winner will handle the message
                    if (singleFinished != null && singleFinished.IsFaulted)
                    {
                        Logger.Log(this.devEUI, $"error in task checking cloud to device message: {singleFinished.Exception?.Message}", LogLevel.Error);
                        return null;
                    }

                    return singleFinished?.Result;
                }
                else
                {
                    // Verbose log as long as this is a new feature
                    Logger.Log(this.devEUI, $"task ReceiveAsync returned by timeout", LogLevel.Debug);

                    // Task.Delay won the race, we are not awaiting for ReceiveAsync anymore
                    await this.receiveAsyncLock.WaitAsync();
                    try
                    {
                        if (localPendingReceiveAsync == this.receiveAsyncTaskCompletionSource)
                        {
                            --this.receiveAsyncTaskWaitCount;
                        }
                    }
                    finally
                    {
                        this.receiveAsyncLock.Release();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Setups a new <see cref="receiveAsyncTaskCompletionSource"/>. Must be called while the lock is owned
        /// </summary>
        private TaskCompletionSource<Message> SetupNewReceiveAsyncTaskCompletionSource(TimeSpan timeout)
        {
            var localPendingReceiveAsync = this.receiveAsyncTaskCompletionSource = new TaskCompletionSource<Message>();
            this.receiveAsyncTaskWaitCount = 0;

            // Verbose log as long as this is a new feature
            Logger.Log(this.devEUI, $"starting new ReceiveAsync task", LogLevel.Debug);

            _ = this.InternalReceiveAsync(timeout).ContinueWith(async (t) =>
            {
                var hasReceivers = false;
                // if no one cares abandon message
                await this.receiveAsyncLock.WaitAsync();
                try
                {
                    hasReceivers = this.receiveAsyncTaskWaitCount > 0;

                    if (!hasReceivers && localPendingReceiveAsync == this.receiveAsyncTaskCompletionSource)
                    {
                        this.receiveAsyncTaskCompletionSource = null;
                        this.receiveAsyncTaskWaitCount = 0;
                    }
                }
                finally
                {
                    this.receiveAsyncLock.Release();
                }

                // Verbose log as long as this is a new feature
                Logger.Log(this.devEUI, $"finished ReceiveAsync task (hasReceivers={hasReceivers})", LogLevel.Debug);

                if (hasReceivers)
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        localPendingReceiveAsync.SetResult(t.Result);
                    }
                    else
                    {
                        localPendingReceiveAsync.SetException(t.Exception);
                    }
                }
                else if (t.IsCompletedSuccessfully && t.Result != null)
                {
                    // Verbose log as long as this is a new feature
                    Logger.Log(this.devEUI, $"task ReceiveAsync found message but not one is awaiting, abandoning", LogLevel.Debug);

                    try
                    {
                        await this.AbandonAsync(t.Result);
                    }
                    catch (Exception ex)
                    {
                        Logger.Log(this.devEUI, $"failed to abandon message from task without listener: {ex.Message}", LogLevel.Error);
                    }
                }
            });

            return localPendingReceiveAsync;
        }

        public async Task<Message> InternalReceiveAsync(TimeSpan timeout)
        {
            try
            {
                // ReceiveAsync only respects OperationTimeoutInMilliseconds
                this.deviceClient.OperationTimeoutInMilliseconds = 60_000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"checking cloud to device message for {timeout}", LogLevel.Debug);

                Message msg = await this.deviceClient.ReceiveAsync(timeout);

                if (Logger.LoggerLevel >= LogLevel.Debug)
                {
                    if (msg == null)
                        Logger.Log(this.devEUI, "done checking cloud to device message, found no message", LogLevel.Debug);
                    else
                        Logger.Log(this.devEUI, $"done checking cloud to device message, found message id: {msg.MessageId ?? "undefined"}", LogLevel.Debug);
                }

                return msg;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not retrieve cloud to device message with error: {ex.Message}", LogLevel.Error);
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
                this.deviceClient.OperationTimeoutInMilliseconds = 60_000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"completing cloud to device message, id: {message.MessageId ?? "undefined"}", LogLevel.Debug);

                await this.deviceClient.CompleteAsync(message);

                Logger.Log(this.devEUI, $"done completing cloud to device message, id: {message.MessageId ?? "undefined"}", LogLevel.Debug);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not complete cloud to device message (id: {message.MessageId ?? "undefined"}) with error: {ex.Message}", LogLevel.Error);
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
                this.deviceClient.OperationTimeoutInMilliseconds = 60_000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"abandoning cloud to device message, id: {message.MessageId ?? "undefined"}", LogLevel.Debug);

                await this.deviceClient.AbandonAsync(message);

                Logger.Log(this.devEUI, $"done abandoning cloud to device message, id: {message.MessageId ?? "undefined"}", LogLevel.Debug);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not abandon cloud to device message (id: {message.MessageId ?? "undefined"}) with error: {ex.Message}", LogLevel.Error);
                return false;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                this.SetRetry(false);
            }
        }

        public async Task<bool> RejectAsync(Message message)
        {
            try
            {
                this.deviceClient.OperationTimeoutInMilliseconds = 60_000;

                this.SetRetry(true);

                Logger.Log(this.devEUI, $"rejecting cloud to device message, id: {message.MessageId ?? "undefined"}", LogLevel.Debug);

                await this.deviceClient.RejectAsync(message);

                Logger.Log(this.devEUI, $"done rejecting cloud to device message, id: {message.MessageId ?? "undefined"}", LogLevel.Debug);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not reject cloud to device message (id: {message.MessageId ?? "undefined"}) with error: {ex.Message}", LogLevel.Error);
                return false;
            }
            finally
            {
                // disable retry, this allows the server to close the connection if another gateway tries to open the connection for the same device
                this.SetRetry(false);
            }
        }

        /// <summary>
        /// Disconnects device client
        /// </summary>
        public bool Disconnect()
        {
            try
            {
                if (this.deviceClient != null)
                {
                    this.deviceClient.Dispose();
                    this.deviceClient = null;

                    Logger.Log(this.devEUI, "device client disconnected", LogLevel.Debug);
                }
                else
                {
                    Logger.Log(this.devEUI, "device client was already disconnected", LogLevel.Debug);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(this.devEUI, $"could not disconnect device client with error: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        /// Ensures that the connection is open
        /// </summary>
        public bool EnsureConnected()
        {
            if (this.deviceClient == null)
            {
                try
                {
                    this.deviceClient = this.deviceFactory.CreateDeviceClient(this.connectionString);
                    Logger.Log(this.devEUI, "device client reconnected", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    Logger.Log(this.devEUI, $"could not connect device client with error: {ex.Message}", LogLevel.Error);
                    return false;
                }
            }

            return true;
        }

        public void Dispose()
        {
            this.deviceClient?.Dispose();
            this.deviceClient = null;

            this.receiveAsyncLock?.Dispose();
            this.receiveAsyncLock = null;

            GC.SuppressFinalize(this);
        }
    }
}