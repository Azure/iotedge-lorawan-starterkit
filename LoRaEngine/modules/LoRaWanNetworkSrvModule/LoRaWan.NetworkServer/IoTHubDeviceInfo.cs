// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public class IoTHubDeviceInfo
    {
        public string DevAddr { get; set; }

        public string DevEUI { get; set; }

        public string PrimaryKey { get; set; }

        public string GatewayId { get; set; }

        public string NwkSKey { get; set; }

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
