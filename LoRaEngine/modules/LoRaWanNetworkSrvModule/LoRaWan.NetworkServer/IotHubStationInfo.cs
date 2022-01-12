// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using Newtonsoft.Json;

    public sealed class IotHubStationInfo
    {
        [JsonProperty("DevEUI")]
        public string StationEuiString
        {
            get => StationEui.ToHex();
            set => StationEui = StationEui.Parse(value);
        }

        [JsonIgnore]
        public StationEui StationEui { get; set; }

        public string PrimaryKey { get; set; }

        public IotHubStationInfo(string stationEuiString, string primaryKey)
        {
            StationEuiString = stationEuiString;
            PrimaryKey = primaryKey;
        }
    }
}
