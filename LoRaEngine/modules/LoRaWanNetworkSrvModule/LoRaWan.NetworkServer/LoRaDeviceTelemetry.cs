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
        public byte PortByte { get => (byte)Port; set => Port = (FramePort)value; }

        [JsonIgnore]
        public FramePort Port { get; set; }

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
            if (rxpk is null) throw new ArgumentNullException(nameof(rxpk));
            if (upstreamPayload is null) throw new ArgumentNullException(nameof(upstreamPayload));
            if (rxpk.ExtraData != null)
                ExtraData = new Dictionary<string, object>(rxpk.ExtraData);
            Chan = rxpk.Chan;
            Codr = rxpk.Codr;
            Data = payloadData;
            Rawdata = decryptedPayloadData?.Length > 0 ? Convert.ToBase64String(decryptedPayloadData) : string.Empty;
            Datr = rxpk.Datr;
            Freq = rxpk.Freq;
            Lsnr = rxpk.Lsnr;
            Modu = rxpk.Modu;
            Rfch = rxpk.Rfch;
            Rssi = rxpk.Rssi;
            Size = rxpk.Size;
            Stat = rxpk.Stat;
            Time = rxpk.Time;
            Tmms = rxpk.Tmms;
            Tmst = rxpk.Tmst;
            Fcnt = upstreamPayload.GetFcnt();
            Port = upstreamPayload.Fport.Value;
        }
    }
}
