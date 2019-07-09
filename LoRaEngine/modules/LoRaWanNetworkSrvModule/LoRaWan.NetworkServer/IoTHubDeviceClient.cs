// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    /// <summary>
    /// Wrapper of <see cref="DeviceClient"/>
    /// </summary>
    internal sealed class IoTHubDeviceClient : IIoTHubDeviceClient
    {
        private DeviceClient deviceClient;

        public IoTHubDeviceClient(DeviceClient deviceClient)
        {
            this.deviceClient = deviceClient ?? throw new System.ArgumentNullException(nameof(deviceClient));
        }

        public uint OperationTimeoutInMilliseconds
        {
            get => this.deviceClient.OperationTimeoutInMilliseconds;
            set => this.deviceClient.OperationTimeoutInMilliseconds = value;
        }

        public Task AbandonAsync(Message message) => this.deviceClient.AbandonAsync(message);

        public Task CompleteAsync(Message message) => this.deviceClient.CompleteAsync(message);

        public void Dispose() => this.deviceClient.Dispose();

        public Task<Twin> GetTwinAsync() => this.deviceClient.GetTwinAsync();

        public Task<Message> ReceiveAsync(TimeSpan timeout) => this.deviceClient.ReceiveAsync(timeout);

        public Task RejectAsync(Message message) => this.deviceClient.RejectAsync(message);

        public Task SendEventAsync(Message message) => this.deviceClient.SendEventAsync(message);

        public void SetRetryPolicy(IRetryPolicy retryPolicy) => this.deviceClient.SetRetryPolicy(retryPolicy);

        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) => this.deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
    }
}