// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using LoRaWan;
    using Newtonsoft.Json.Linq;

    internal class QueryDeviceTwin : IDeviceTwin
    {
        public string DeviceId => deviceTwin["$id"].ToString();

        private readonly JObject deviceTwin;
        private readonly string componentName;

        public QueryDeviceTwin(string componentName, JObject result)
        {
            this.deviceTwin = result;
            this.componentName = componentName;
        }

        public DevAddr GetDevAddr()
        {
            if (DevAddr.TryParse(deviceTwin[$"{componentName}.DevAddr"].ToString(), out var someDevAddr))
            {
                return someDevAddr;
            }

            throw new LoRaProcessingException($"Dev addr '{deviceTwin[$"{componentName}.DevAddr"]}' is invalid.", LoRaProcessingErrorCode.InvalidFormat);
        }

        public string GetGatewayID()
        {
            return deviceTwin[$"{componentName}.GatewayID"].ToString();
        }

        public DateTime GetLastUpdated()
        {
            // At this time, there is no way to get Last update date from IoT Central
            return DateTime.Now;
        }

        public string GetNwkSKey()
        {
            return deviceTwin[$"{componentName}.NwkSKey"].ToString();
        }
    }
}
