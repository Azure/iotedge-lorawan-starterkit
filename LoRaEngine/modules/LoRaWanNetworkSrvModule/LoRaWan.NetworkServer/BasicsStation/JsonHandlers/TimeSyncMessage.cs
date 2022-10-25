// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{

    using LoRaWan.NetworkServer.BasicsStation;

    internal class TimeSyncMessage
    {
        public ulong txtime { get; set; }
        public ulong gpstime { get; set; }
        public string msgtype { get; set; }
    }
}
