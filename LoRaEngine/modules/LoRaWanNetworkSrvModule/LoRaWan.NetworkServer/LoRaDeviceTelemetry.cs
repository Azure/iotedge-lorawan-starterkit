// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Collections.Generic;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using Newtonsoft.Json;

    // Represents the device telemetry that will be sent to IoT Hub
    public class LoRaDeviceTelemetry
    {
        [JsonProperty("time")]
        public string Time { get; set; }

        [JsonProperty("tmms")]
        public uint Tmms { get; set; }

        [JsonProperty("tmst")]
        public uint Tmst { get; set; }

        [JsonProperty("freq")]
        public double Freq { get; set; }

        [JsonProperty("chan")]
        public uint Chan { get; set; }

        [JsonProperty("rfch")]
        public uint Rfch { get; set; }

        [JsonProperty("stat")]
        public int Stat { get; set; }

        [JsonProperty("modu")]
        public string Modu { get; set; }

        [JsonProperty("datr")]
        public string Datr { get; set; }

        [JsonProperty("codr")]
        public string Codr { get; set; }

        [JsonProperty("rssi")]
        public int Rssi { get; set; }

        [JsonProperty("lsnr")]
        public float Lsnr { get; set; }

        [JsonProperty("size")]
        public uint Size { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("port")]
        public byte Port { get; set; }

        [JsonProperty("fcnt")]
        public ushort Fcnt { get; set; }

        [JsonProperty("rawdata")]
        public string Rawdata { get; set; }

        [JsonProperty("eui")]
        public string DeviceEUI { get; set; }

        [JsonProperty("gatewayid")]
        public string GatewayID { get; set; }

        [JsonProperty("edgets")]
        public long Edgets { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtraData { get; } = new Dictionary<string, object>();

        public LoRaDeviceTelemetry()
        {
        }

        public LoRaDeviceTelemetry(Rxpk rxpk, LoRaPayloadData loRaPayloadData, object payloadData)
        {
            if (rxpk.ExtraData != null)
                this.ExtraData = new Dictionary<string, object>(rxpk.ExtraData);

            this.Chan = rxpk.chan;
            this.Codr = rxpk.codr;
            this.Data = payloadData;
            this.Rawdata = rxpk.data;
            this.Datr = rxpk.datr;
            this.Freq = rxpk.freq;
            this.Lsnr = rxpk.lsnr;
            this.Modu = rxpk.modu;
            this.Rfch = rxpk.rfch;
            this.Rssi = rxpk.rssi;
            this.Size = rxpk.size;
            this.Stat = rxpk.stat;
            this.Time = rxpk.time;
            this.Tmms = rxpk.tmms;
            this.Tmst = rxpk.tmst;
            this.Fcnt = loRaPayloadData.GetFcnt();
            this.Port = loRaPayloadData.GetFPort();
        }
    }
}