// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;

    public interface ILoRaDeviceFactory
    {
        // Creates and initializes a new lora device
        LoRaDevice Create(IoTHubDeviceInfo deviceInfo);
    }
}