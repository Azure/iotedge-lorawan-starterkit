//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public interface ILoRaDeviceFrameCounterUpdateStrategy
    {
        Task ResetAsync(ILoRaDevice loraDeviceInfo);
        ValueTask<int> NextFcntDown(ILoRaDevice loraDeviceInfo);
        Task UpdateAsync(ILoRaDevice loraDeviceInfo);
    }

}