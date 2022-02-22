// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;

    public interface ILoRaDeviceClientConnectionManager : IDisposable
    {
        ILoRaDeviceClient GetClient(LoRaDevice loRaDevice);

        void Release(LoRaDevice loRaDevice);

        void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient);

        IAsyncDisposable BeginDeviceClientConnectionActivity(LoRaDevice loRaDevice);
    }
}
