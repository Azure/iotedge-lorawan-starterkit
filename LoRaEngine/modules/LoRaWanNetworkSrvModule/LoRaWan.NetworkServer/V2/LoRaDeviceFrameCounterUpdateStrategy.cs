//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
    public interface ILoRaDeviceFrameCounterUpdateStrategy
    {
        Task ResetAsync(LoRaDevice loraDeviceInfo);
        ValueTask<int> NextFcntDown(LoRaDevice loraDeviceInfo);
        Task UpdateAsync(LoRaDevice loraDeviceInfo);
        void InitializeDeviceFrameCount(LoRaDevice loraDevice);
    }

}