// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;

    public interface ILoRaDeviceRegistry
    {
        /// <summary>
        /// Gets devices that matches an OTAA join request
        /// </summary>
        Task<LoRaDevice> GetDeviceForJoinRequestAsync(string devEUI, string appEUI, string devNonce);

        /// <summary>
        /// Updates device after a succesfull join request
        /// </summary>
        void UpdateDeviceAfterJoin(LoRaDevice loRaDevice, string oldDevAddr = null);

        /// <summary>
        /// Registers a <see cref="ILoRaDeviceInitializer"/>
        /// </summary>
        void RegisterDeviceInitializer(ILoRaDeviceInitializer initializer);

        /// <summary>
        /// Resets the device cache
        /// </summary>
        void ResetDeviceCache();

        /// <summary>
        /// Gets a <see cref="ILoRaDeviceRequestQueue"/> where requests can be queued
        /// </summary>
        ILoRaDeviceRequestQueue GetLoRaRequestQueue(LoRaRequest request);

        /// <summary>
        /// Gets a device by DevEUI
        /// </summary>
        Task<LoRaDevice> GetDeviceByDevEUIAsync(string devEUI);
    }
}