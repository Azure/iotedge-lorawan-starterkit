//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using LoRaTools.LoRaPhysical;
using Newtonsoft.Json;

namespace LoRaWan.NetworkServer.V2
{
    // Represents the device telemetry that will be sent to IoT Hub
    public class DeviceTelemetry : Rxpk
    {
        [JsonProperty("eui")]
        public string DeviceEUI;

        [JsonProperty("gatewayid")]
        public string GatewayID;

        [JsonProperty("edgets")]
        public long Edgets;
        

        public DeviceTelemetry(Rxpk rxpk) : base(rxpk)
        {
        }
    }
}