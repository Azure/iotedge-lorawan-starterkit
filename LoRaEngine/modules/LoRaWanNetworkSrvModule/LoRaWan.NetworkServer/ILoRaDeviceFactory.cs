// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using Microsoft.Azure.Devices.Client;

    public interface ILoRaDeviceFactory
    {
        /// <summary>
        /// Creates and initializes a new lora device
        /// </summary>
        LoRaDevice Create(IoTHubDeviceInfo deviceInfo);

        /// <summary>
        /// Creates a new instance of <see cref="IIoTHubDeviceClient"/>
        /// </summary>
        IIoTHubDeviceClient CreateDeviceClient(string connectionString);
    }
}