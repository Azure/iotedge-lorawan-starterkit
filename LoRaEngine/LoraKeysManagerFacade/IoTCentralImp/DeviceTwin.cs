// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaWan;

    public sealed class DeviceTwin : IDeviceTwin
    {
        private readonly DesiredProperties properties;

        public DeviceTwin(string deviceName, DesiredProperties properties)
        {
            this.DeviceId = deviceName;
            this.properties = properties;
        }

        public string DeviceId { get; }

        public DevAddr DevAddr
        {
            get
            {
                if (DevAddr.TryParse(this.properties.DevAddr, out var someDevAddr))
                {
                    return someDevAddr;
                }

                return new DevAddr();
            }
        }

        public string GatewayID => this.properties.GatewayID;

        public DateTime LastUpdated
        {
            get
            {
                if (!this.properties.AdditionalData.TryGetValue("$metadata", out var metadata))
                {
                    return DateTime.MinValue;
                }

                return new List<DateTime>()
                {
                    metadata?["NwkSKey"]?["lastUpdateTime"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                    metadata?["DevAddr"]?["lastUpdateTime"]?.ToObject<DateTime>() ?? DateTime.MinValue,
                    metadata?["GatewayID"]?["lastUpdateTime"]?.ToObject<DateTime>() ?? DateTime.MinValue
                }
                .OrderByDescending(x => x)
                .First();
            }
        }

        public string NwkSKey => this.properties.NwkSKey;

    }
}
