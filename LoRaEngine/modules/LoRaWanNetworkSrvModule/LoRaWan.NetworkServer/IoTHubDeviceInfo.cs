// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Newtonsoft.Json;

    public class IoTHubDeviceInfo
    {
        [JsonProperty("DevAddr")]
        public string DevAddrString
        {
            get => DevAddr?.ToString();
            set => DevAddr = value is { Length: 8 } some ? LoRaWan.DevAddr.Parse(some) : null;
        }

        public DevAddr? DevAddr { get; set; }

        public string DevEUI { get; set; }

        public string PrimaryKey { get; set; }

        public string GatewayId { get; set; }

        public string NwkSKey { get; set; }

        public IoTHubDeviceInfo()
        {
        }

        public IoTHubDeviceInfo(DevAddr? devAddr, string devEUI, string primaryKey)
        {
            DevAddr = devAddr;
            DevEUI = devEUI;
            PrimaryKey = primaryKey;
        }
    }
}
