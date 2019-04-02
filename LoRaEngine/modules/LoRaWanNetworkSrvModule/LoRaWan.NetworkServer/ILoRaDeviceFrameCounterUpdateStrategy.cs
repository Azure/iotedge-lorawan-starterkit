// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;

    // Defines a contract to handle device frame counters
    // A device has 2 frame counters
    // Up:   messages coming from the device (D2C)
    // Down: messages going to the device (C2D)
    public interface ILoRaDeviceFrameCounterUpdateStrategy
    {
        // Resets the frame count
        Task<bool> ResetAsync(LoRaDevice loRaDevice, uint fcntUp, string gatewayId);

        // Resolves next frame down counter
        ValueTask<uint> NextFcntDown(LoRaDevice loRaDevice, uint messageFcnt);

        // Save changes to the frame counter
        Task<bool> SaveChangesAsync(LoRaDevice loRaDevice);
    }
}