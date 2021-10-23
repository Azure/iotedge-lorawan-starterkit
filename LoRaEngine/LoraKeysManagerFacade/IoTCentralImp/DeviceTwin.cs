// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class DeviceTwin : IDeviceTwin
    {
        private readonly DesiredProperties properties;

        public DeviceTwin(string deviceName, DesiredProperties properties)
        {
            this.DeviceId = deviceName;
            this.properties = properties;
        }

        public string DeviceId { get; }

        public string GetDevAddr()
        {
            return this.properties.DevAddr;
        }

        public string GetGatewayID()
        {
            return this.properties.GatewayID;
        }

        public DateTime GetLastUpdated()
        {
            if (!this.properties.AdditionalData.ContainsKey("$metadata"))
            {
                return DateTime.MinValue;
            }

            return new List<DateTime>()
            {
                this.properties.AdditionalData["$metadata"]?["NwkSKey"]?["lastUpdateTime"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                this.properties.AdditionalData["$metadata"]?["DevAddr"]?["lastUpdateTime"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                this.properties.AdditionalData["$metadata"]?["GatewayID"]?["lastUpdateTime"]?.ToObject<DateTime>() ?? DateTime.MinValue
            }
            .OrderByDescending(x => x)
            .First();
        }

        public string GetNwkSKey()
        {
            return this.properties.NwkSKey;
        }
    }
}
