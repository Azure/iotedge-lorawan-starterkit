// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using LoRaWan;
    using Newtonsoft.Json;

    public class IoTHubDeviceInfo
    {
        [JsonProperty("DevAddr")]
        public string DevAddrString
        {
            get => DevAddr.ToString();
            set => DevAddr = DevAddr.Parse(value);
        }

        [JsonIgnore]
        public DevAddr DevAddr { get; set; }

        [JsonIgnore]
        public DevEui? DevEUI { get; set; }

        [JsonProperty("DevEUI")]
        public string DevEuiString
        {
            get => DevEUI?.ToString();
            set => DevEUI = value is null ? null : DevEui.Parse(value);
        }

        public string PrimaryKey { get; set; }
    }
}
