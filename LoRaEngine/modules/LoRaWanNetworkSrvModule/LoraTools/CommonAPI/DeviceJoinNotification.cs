// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.CommonAPI
{
    using LoRaWan;
    using Newtonsoft.Json;

    public class DeviceJoinNotification
    {
        public string GatewayId { get; set; }

        [JsonIgnore]
        public DevAddr DevAddr { get; set; }

        [JsonIgnore]
        public DevEui? DevEUI { get; set; }

        [JsonIgnore]
        public NetworkSessionKey? NwkSKey { get; set; }

        [JsonProperty(nameof(DevAddr))]
        public string DevAddrString
        {
            get => DevAddr.ToString();
            set => DevAddr = DevAddr.Parse(value);
        }

        [JsonProperty(nameof(DevEUI))]
        public string DevEuiString
        {
            get => DevEUI?.ToString();
            set => DevEUI = value is null ? null : DevEui.Parse(value);
        }

        [JsonProperty(nameof(NwkSKey))]
        public string NwkSKeyString
        {
            get => NwkSKey?.ToString();
            set => NwkSKey = value is null ? null : NetworkSessionKey.Parse(value);
        }
    }
}
