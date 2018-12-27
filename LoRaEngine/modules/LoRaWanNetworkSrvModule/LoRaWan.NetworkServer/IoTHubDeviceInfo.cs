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
