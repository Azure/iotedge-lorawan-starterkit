// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;

    public interface ILoRaDeviceClientConnectionManager : IAsyncDisposable
    {
        ILoRaDeviceClient GetClient(LoRaDevice loRaDevice);

        Task ReleaseAsync(LoRaDevice loRaDevice);

        void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient);

        IAsyncDisposable BeginDeviceClientConnectionActivity(LoRaDevice loRaDevice);
    }
}
