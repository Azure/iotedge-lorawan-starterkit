//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//


using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
    public interface ILoRaDeviceFactory
    {
        // Creates and initializes a new lora device
        LoRaDevice Create(IoTHubDeviceInfo deviceInfo);
        Task InitializeAsync(LoRaDevice loraDevice);
    }
}