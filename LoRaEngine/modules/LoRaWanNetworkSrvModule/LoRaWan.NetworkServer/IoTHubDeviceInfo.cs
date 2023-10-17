// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Newtonsoft.Json;

    public class IoTHubDeviceInfo
    {
        [JsonProperty(nameof(DevAddr))]
        public string DevAddrString
        {
            get => DevAddr?.ToString();
            set => DevAddr = value is { Length: 8 } some ? LoRaWan.DevAddr.Parse(some) : null;
        }

        [JsonIgnore]
        public DevAddr? DevAddr { get; set; }

        [JsonProperty(nameof(DevEUI))]
        public string DevEuiString
        {
            get => DevEUI.ToString();
            set => DevEUI = DevEui.Parse(value);
        }

        [JsonIgnore]
        public DevEui DevEUI { get; set; }

        public string PrimaryKey { get; set; }

        public string GatewayId { get; set; }

        [JsonProperty(nameof(NwkSKey))]
        public string NwkSKeyString
        {
            get => NwkSKey?.ToString();
            set => NwkSKey = value is null ? null : NetworkSessionKey.Parse(value);
        }

        [JsonIgnore]
        public NetworkSessionKey? NwkSKey { get; set; }

        public IoTHubDeviceInfo()
        {
        }

        public IoTHubDeviceInfo(DevAddr? devAddr, DevEui devEui, string primaryKey)
        {
            DevAddr = devAddr;
            DevEUI = devEui;
            PrimaryKey = primaryKey;
        }
    }
}
