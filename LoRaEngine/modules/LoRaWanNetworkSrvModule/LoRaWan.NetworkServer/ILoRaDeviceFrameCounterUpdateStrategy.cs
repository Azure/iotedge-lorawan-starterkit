//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    // Defines a contract to handle device frame counters
    // A device has 2 frame counters
    // Up:   messages coming from the device (D2C)
    // Down: messages going to the device (C2D)
    public interface ILoRaDeviceFrameCounterUpdateStrategy
    {
        // Resets the frame count
        Task<bool> ResetAsync(LoRaDevice loRaDevice);

        // Resolves next frame down counter
        ValueTask<int> NextFcntDown(LoRaDevice loRaDevice, int messageFcnt);

        // Save changes to the frame counter
        Task<bool> SaveChangesAsync(LoRaDevice loRaDevice);
    }
}