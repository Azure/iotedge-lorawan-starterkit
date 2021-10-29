// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public interface ILoRaDeviceFactory
    {
        // Creates and initializes a new lora device
        LoRaDevice Create(IoTHubDeviceInfo deviceInfo);

        /// <summary>
        /// Creates a device client based on the devEUI and primary key.
        /// </summary>
        /// <param name="eui">Dev EUI of the device.</param>
        /// <param name="primaryKey">Primary key of the device.</param>
        ILoRaDeviceClient CreateDeviceClient(string eui, string primaryKey);
    }
}
