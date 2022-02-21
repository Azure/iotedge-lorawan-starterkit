// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILoRaDeviceClientConnectionManager : IDisposable
    {
        bool EnsureConnected(LoRaDevice loRaDevice);

        ILoRaDeviceClient GetClient(LoRaDevice loRaDevice);

        void CloseConnection(LoRaDevice loRaDevice);

        void Release(LoRaDevice loRaDevice);

        void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient);
        Task CloseConnectionAsync(LoRaDevice loRaDevice, CancellationToken cancellationToken);
    }
}
