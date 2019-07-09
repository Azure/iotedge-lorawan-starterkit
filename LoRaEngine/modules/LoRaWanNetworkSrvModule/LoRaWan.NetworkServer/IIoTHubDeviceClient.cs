// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    /// <summary>
    /// Abstraction of <see cref="DeviceClient"/>
    /// </summary>
    public interface IIoTHubDeviceClient : IDisposable
    {
        uint OperationTimeoutInMilliseconds { get; set; }

        void SetRetryPolicy(IRetryPolicy retryPolicy);

        Task<Twin> GetTwinAsync();

        Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties);

        Task SendEventAsync(Message message);

        Task<Message> ReceiveAsync(TimeSpan timeout);

        Task CompleteAsync(Message message);

        Task AbandonAsync(Message message);

        Task RejectAsync(Message message);
    }
}