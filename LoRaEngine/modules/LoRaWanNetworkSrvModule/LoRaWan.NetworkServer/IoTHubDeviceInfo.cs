// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Newtonsoft.Json;

    public class IoTHubDeviceInfo
    {
        public string DevAddr { get; set; }

        public string DevEUI { get; set; }

        public string PrimaryKey { get; set; }

        public string GatewayId { get; set; }

        [JsonProperty("NwkSKey")]
        public string NwkSKeyString
        {
            get => NwkSKey?.ToString();
            set => NwkSKey = string.IsNullOrEmpty(value) ? null : NetworkSessionKey.Parse(value);
        }

        [JsonIgnore]
        public NetworkSessionKey? NwkSKey { get; set; }

        public IoTHubDeviceInfo()
        {
        }

        public IoTHubDeviceInfo(string devAddr, string devEUI, string primaryKey)
        {
            DevAddr = devAddr;
            DevEUI = devEUI;
            PrimaryKey = primaryKey;
        }
    }
}
