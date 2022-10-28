// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System.Text.Json.Serialization;
    using LoRaWan.NetworkServer.BasicsStation;

    internal class TimeSyncMessage
    {
        [JsonPropertyName("txtime")]
        public ulong TxTime { get; set; }

        [JsonPropertyName("gpstime")]
        public ulong GpsTime { get; set; }

        [JsonPropertyName("msgtype")]
        public string MsgType { get; set; }
    }
}
