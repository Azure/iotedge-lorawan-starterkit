// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
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

        [JsonProperty("dupmsg", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? DupMsg { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtraData { get; } = new Dictionary<string, object>();

        public LoRaDeviceTelemetry()
        {
        }

        public LoRaDeviceTelemetry(Rxpk rxpk, LoRaPayloadData upstreamPayload, object payloadData, byte[] decryptedPayloadData)
        {
            if (rxpk.ExtraData != null)
                this.ExtraData = new Dictionary<string, object>(rxpk.ExtraData);

            this.Chan = rxpk.Chan;
            this.Codr = rxpk.Codr;
            this.Data = payloadData;
            this.Rawdata = decryptedPayloadData?.Length > 0 ? Convert.ToBase64String(decryptedPayloadData) : string.Empty;
            this.Datr = rxpk.Datr;
            this.Freq = rxpk.Freq;
            this.Lsnr = rxpk.Lsnr;
            this.Modu = rxpk.Modu;
            this.Rfch = rxpk.Rfch;
            this.Rssi = rxpk.Rssi;
            this.Size = rxpk.Size;
            this.Stat = rxpk.Stat;
            this.Time = rxpk.Time;
            this.Tmms = rxpk.Tmms;
            this.Tmst = rxpk.Tmst;
            this.Fcnt = upstreamPayload.GetFcnt();
            this.Port = upstreamPayload.GetFPort();
        }
    }
}