//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using LoRaTools.LoRaPhysical;
using Newtonsoft.Json;

namespace LoRaWan.NetworkServer.V2
{
    // Represents the device telemetry that will be sent to IoT Hub
    public class LoRaDeviceTelemetry
    {
        public string time;
        public uint tmms;
        public uint tmst;
        public double freq;
        public uint chan;
        public uint rfch;
        public int stat;
        public string modu;
        public string datr;
        public string codr;
        public int rssi;
        public float lsnr;
        public uint size;
        public object data;

        [JsonProperty("eui")]
        public string DeviceEUI;

        [JsonProperty("gatewayid")]
        public string GatewayID;

        [JsonProperty("edgets")]
        public long Edgets;

        [JsonExtensionData]
        public Dictionary<string, object> ExtraData { get; } = new Dictionary<string, object>();


        public LoRaDeviceTelemetry()
        {    
        }        

        public LoRaDeviceTelemetry(Rxpk rxpk)
        {         
            if (rxpk.ExtraData != null)
                this.ExtraData = new Dictionary<string, object>(rxpk.ExtraData);

            this.chan = rxpk.chan;
            this.codr = rxpk.codr;
            this.data = rxpk.data;
            this.datr = rxpk.datr;
            this.freq = rxpk.freq;
            this.lsnr = rxpk.lsnr;
            this.modu = rxpk.modu;
            this.rfch = rxpk.rfch;
            this.rssi = rxpk.rssi;
            this.size = rxpk.size;
            this.stat = rxpk.stat;
            this.time = rxpk.time;
            this.tmms = rxpk.tmms;
            this.tmst = rxpk.tmst;        
        }
    }
}