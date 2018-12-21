//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public interface ILoRaDevice
    {
        string GatewayID { get; set; }
        UInt32 FcntUp { get; }
        string AppSKey { get; set; }
        uint? ReceiveDelay1 { get; }
        uint? ReveiveDelay2 { get; }
        bool AlwaysUseSecondWindow { get; }

        bool IsABP();
        bool WasNotJustReadFromCache();
        UInt32 IncrementFcntDown(UInt32 v);
        bool IsABPRelaxedFrameCounter();
        void SetFcntUp(UInt32 v);
        void SetFcntDown(UInt32 v);

        Task<Message> ReceiveCloudToDeviceAsync(TimeSpan waitTime);
        object CompleteCloudToDeviceMessageAsync(Message c2dMsg);
        object AbandonCloudToDeviceMessageAsync(Message additionalMsg);
        Task UpdateTwinAsync(object twinProperties);
        Dictionary<string, object> GetTwinProperties();
    }

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


        Task<ILoRaDevice> GetDeviceForPayloadAsync(LoRaTools.LoRaMessage.LoRaPayloadData loraPayload);
    }
}