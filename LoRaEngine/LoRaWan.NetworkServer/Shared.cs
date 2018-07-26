using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer
{
    public static class Shared
    {
        public static ConcurrentDictionary<string, LoraDeviceInfo> loraDeviceInfoList = new ConcurrentDictionary<string, LoraDeviceInfo>();
        public static ConcurrentDictionary<string, LoraDeviceInfo> loraJoinRequestList = new ConcurrentDictionary<string, LoraDeviceInfo>();
        public static string DeviceConnectionString;
    }
}
