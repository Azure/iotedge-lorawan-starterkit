﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer.V2
{
    public interface ILoRaDeviceRegistry
    {
        // Going to search devices in
        // 1. Cache
        // 2. If offline -> local storage (future functionality, reverse lookup)
        // 3. If online -> call function (return all devices with matching devaddr)
        // 3.1 Validate [mic, gatewayid]

        // In order to handle a scenario where the network server is restarted and the fcntDown was not yet saved (we save every 10)
        // If device does not have gatewayid this will be handled by the service facade function (NextFCntDown)
        // 4. if (loraDeviceInfo.IsABP() && loraDeviceInfo.GatewayID != null && loraDeviceInfo was not read from cache)  device.FcntDown += 10;


        Task<LoRaDevice> GetDeviceForPayloadAsync(LoRaTools.LoRaMessage.LoRaPayloadData loraPayload);


        /// <summary>
        /// Gets devices that matches an OTAA join request
        /// </summary>
        /// <param name="devEUI"></param>
        /// <param name="appEUI"></param>
        /// <param name="devNonce"></param>
        /// <returns></returns>
        Task<LoRaDevice> GetDeviceForJoinRequestAsync(string devEUI, string appEUI, string devNonce);

        /// <summary>
        /// Updates device after a succesfull join request
        /// </summary>
        /// <param name="loRaDevice"></param>
        void UpdateDeviceAfterJoin(LoRaDevice loRaDevice);


        /// <summary>
        /// Registers a <see cref="ILoRaDeviceInitializer"/>
        /// </summary>
        /// <param name="initializer"></param>
        void RegisterDeviceInitializer(ILoRaDeviceInitializer initializer);
    }
}