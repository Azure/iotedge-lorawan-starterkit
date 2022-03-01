// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading.Tasks;

    public interface ILoRaDeviceRegistry : IAsyncDisposable
    {
        /// <summary>
        /// Gets devices that matches an OTAA join request.
        /// </summary>
        Task<LoRaDevice> GetDeviceForJoinRequestAsync(DevEui devEUI, DevNonce devNonce);

        /// <summary>
        /// Updates device after a succesfull join request.
        /// </summary>
        void UpdateDeviceAfterJoin(LoRaDevice loRaDevice, DevAddr? oldDevAddr = null);

        /// <summary>
        /// Registers a <see cref="ILoRaDeviceInitializer"/>.
        /// </summary>
        void RegisterDeviceInitializer(ILoRaDeviceInitializer initializer);

        /// <summary>
        /// Resets the device cache.
        /// </summary>
        Task ResetDeviceCacheAsync();

        /// <summary>
        /// Gets a <see cref="ILoRaDeviceRequestQueue"/> where requests can be queued.
        /// </summary>
        ILoRaDeviceRequestQueue GetLoRaRequestQueue(LoRaRequest request);

        /// <summary>
        /// Gets a device by DevEUI.
        /// </summary>
        Task<LoRaDevice> GetDeviceByDevEUIAsync(DevEui devEUI);
    }
}
