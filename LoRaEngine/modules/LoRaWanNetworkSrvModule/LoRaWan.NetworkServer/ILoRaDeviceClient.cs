// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;

    /// <summary>
    /// LoRa device client contract
    /// Defines the iteractions between a LoRa device and a IoT service (Azure IoT Hub)
    /// </summary>
    public interface ILoRaDeviceClient : IDisposable
    {
        /// <summary>
        /// Gets the twin properties for the device
        /// </summary>
        Task<Twin> GetTwinAsync();

        /// <summary>
        /// Sends a telemetry/event
        /// </summary>
        Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties);

        /// <summary>
        /// Updates the device reported properties
        /// </summary>
        Task<bool> UpdateReportedPropertiesAsync(TwinCollection reportedProperties);

        /// <summary>
        /// Receive a cloud to device message
        /// </summary>
        Task<Message> ReceiveAsync(TimeSpan timeout);

        /// <summary>
        /// Completes a cloud to device message
        /// </summary>
        Task<bool> CompleteAsync(Message cloudToDeviceMessage);

        /// <summary>
        /// Abandon a cloud to device message
        /// </summary>
        Task<bool> AbandonAsync(Message cloudToDeviceMessage);

        /// <summary>
        /// Reject a cloud to device message
        /// </summary>
        Task<bool> RejectAsync(Message cloudToDeviceMessage);

        /// <summary>
        /// Disconnects device client
        /// </summary>
        bool Disconnect();

        /// <summary>
        /// Ensures the device client is connected
        /// </summary>
        bool EnsureConnected();
    }
}