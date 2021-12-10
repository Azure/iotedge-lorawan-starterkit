// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using LoRaWan;
    using Newtonsoft.Json.Linq;

    public sealed class QueryDeviceTwin : IDeviceTwin
    {
        public string DeviceId => deviceTwin["$id"].ToString();

        private readonly JObject deviceTwin;
        private readonly string componentName;
        private readonly DevAddr devAddr;

        public QueryDeviceTwin(string componentName, JObject result)
        {
            this.deviceTwin = result;
            this.componentName = componentName;

            if (!DevAddr.TryParse(deviceTwin[$"{componentName}.DevAddr"].ToString(), out devAddr))
            {
                throw new LoRaProcessingException($"Dev addr '{deviceTwin[$"{componentName}.DevAddr"]}' is invalid.", LoRaProcessingErrorCode.InvalidFormat);
            }
        }

        public DevAddr DevAddr => this.devAddr;

        public string GatewayID => deviceTwin[$"{componentName}.GatewayID"].ToString();

        // At this time, there is no way to get Last update date from IoT Central
        public DateTime LastUpdated => DateTime.Now;

        public string NwkSKey => deviceTwin[$"{componentName}.NwkSKey"].ToString();
    }
}
