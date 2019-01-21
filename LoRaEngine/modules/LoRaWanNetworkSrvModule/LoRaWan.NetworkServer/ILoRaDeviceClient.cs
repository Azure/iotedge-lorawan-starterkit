//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//


using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// LoRa device client contract
    /// Defines the iteractions between a LoRa device and a IoT service (Azure IoT Hub)
    /// </summary>
    public interface ILoRaDeviceClient : IDisposable
    {
        /// <summary>
        /// Gets the twin properties for the device
        /// </summary>
        /// <returns></returns>
        Task<Twin> GetTwinAsync();

        /// <summary>
        /// Sends a telemetry/event
        /// </summary>
        /// <param name="telemetry"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        Task<bool> SendEventAsync(LoRaDeviceTelemetry telemetry, Dictionary<string, string> properties);

        /// <summary>
        /// Updates the device reported properties
        /// </summary>
        /// <param name="reportedProperties"></param>
        /// <returns></returns>
        Task<bool> UpdateReportedPropertiesAsync(TwinCollection reportedProperties);

        /// <summary>
        /// Receive a cloud to device message 
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        Task<Message> ReceiveAsync(TimeSpan timeout);

        /// <summary>
        /// Completes a cloud to device message
        /// </summary>
        /// <param name="cloudToDeviceMessage"></param>
        /// <returns></returns>
        Task<bool> CompleteAsync(Message cloudToDeviceMessage);

        /// <summary>
        /// Abandon a cloud to device message
        /// </summary>
        /// <param name="cloudToDeviceMessage"></param>
        /// <returns></returns>
        Task<bool> AbandonAsync(Message cloudToDeviceMessage);
    }
}