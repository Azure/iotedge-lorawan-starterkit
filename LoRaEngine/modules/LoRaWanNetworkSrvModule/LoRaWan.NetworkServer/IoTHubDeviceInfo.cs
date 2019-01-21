//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer
{
    public class IoTHubDeviceInfo
    {
        public string DevAddr;
        public string DevEUI;
        public string PrimaryKey;

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
