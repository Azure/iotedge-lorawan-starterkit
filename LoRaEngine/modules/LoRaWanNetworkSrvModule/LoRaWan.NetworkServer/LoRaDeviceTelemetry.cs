// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using LoRaTools.LoRaMessage;
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

        [JsonProperty("modu")]
        public string Modu { get; set; }

        [JsonProperty("datr")]
        public string Datr { get; set; }

        [JsonProperty("rssi")]
        public double Rssi { get; set; }

        [JsonProperty("lsnr")]
        public float Lsnr { get; set; }

        [JsonProperty("data")]
        public object Data { get; set; }

        [JsonProperty("port")]
        public byte PortByte { get => (byte)Port; set => Port = (FramePort)value; }

        [JsonIgnore]
        public FramePort Port { get; set; }

        [JsonProperty("fcnt")]
        public ushort Fcnt { get; set; }

        [JsonProperty("edgets")]
        public long Edgets { get; set; }

        [JsonProperty("rawdata")]
        public string Rawdata { get; set; }

        [JsonProperty("eui")]
        public string DeviceEUI { get; set; }

        [JsonProperty("gatewayid")]
        public string GatewayID { get; set; }


        [JsonProperty("dupmsg", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? DupMsg { get; set; }

        [JsonExtensionData]
        public Dictionary<string, object> ExtraData { get; } = new Dictionary<string, object>();

        public LoRaDeviceTelemetry()
        {
        }

        public LoRaDeviceTelemetry(LoRaRequest request, LoRaPayloadData upstreamPayload, object payloadData, byte[] decryptedPayloadData)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            var radioMetadata = request.RadioMetadata;
            if (radioMetadata is null) throw new ArgumentException(nameof(radioMetadata));
            if (upstreamPayload is null) throw new ArgumentNullException(nameof(upstreamPayload));
            var datr = request.Region.GetDatarateFromIndex(request.RadioMetadata.DataRate);
            Data = payloadData;
            Rawdata = decryptedPayloadData?.Length > 0 ? Convert.ToBase64String(decryptedPayloadData) : string.Empty;
            Fcnt = upstreamPayload.GetFcnt();
            Port = upstreamPayload.Fport.Value;
            Freq = radioMetadata.Frequency.InMega;
            Datr = datr.ToString();
            Rssi = radioMetadata.UpInfo.ReceivedSignalStrengthIndication;
            Rfch = radioMetadata.UpInfo.AntennaPreference;
            Lsnr = radioMetadata.UpInfo.SignalNoiseRatio;
            Time = radioMetadata.UpInfo.Xtime.ToString(CultureInfo.InvariantCulture); // This field was just used for telemetry in rxpk. Now it's being used for bringing unaltered the Xtime.
            Tmst = unchecked((uint)radioMetadata.UpInfo.Xtime); // This is used by former computation only. Should go away when we drop PktFwd support.
            Chan = checked((uint)radioMetadata.DataRate); // This is not used in any computation. It is only reported in the device telemetry.
            Tmms = radioMetadata.UpInfo.GpsTime; // This is not used in any computation. It is only reported in the device telemetry.
            Modu = datr.ModulationKind.ToString(); // This is only used in test path by legacy PacketForwarder code. Safe to eventually remove. Could be also "FSK"
        }
    }
}
