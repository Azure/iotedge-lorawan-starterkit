// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;

    public interface ILoRaDeviceClientConnectionManager : IDisposable
    {
        bool EnsureConnected(LoRaDevice loRaDevice);

        ILoRaDeviceClient GetClient(LoRaDevice loRaDevice);
        ILoRaDeviceClient GetClient(DevEui devEui);

        void Release(LoRaDevice loRaDevice);
        void Release(DevEui devEui);

        void Register(LoRaDevice loRaDevice, ILoRaDeviceClient loraDeviceClient);

        /// <summary>
        /// Tries to trigger scanning of expired items.
        /// </summary>
        void TryScanExpiredItems();
    }
}
