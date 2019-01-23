// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class IoTHubDeviceInfo
    {
        public string DevAddr { get; set; }

        public string DevEUI { get; set; }

        public string PrimaryKey { get; set; }

        public IoTHubDeviceInfo()
        {
        }

        public IoTHubDeviceInfo(string devAddr, string devEUI, string primaryKey)
        {
            this.DevAddr = devAddr;
            this.DevEUI = devEUI;
            this.PrimaryKey = primaryKey;
        }
    }
}
