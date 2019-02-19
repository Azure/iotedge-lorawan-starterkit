// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaPhysical
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using LoRaTools.Regions;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class Rxpk
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
        public string Data { get; set; }

        public double RequiredSnr => this.SpreadFactorToSNR[this.SpreadingFactor];

        public int SpreadingFactor => int.Parse(this.Datr.Substring(this.Datr.IndexOf("SF") + 2, this.Datr.IndexOf("BW") - this.Datr.IndexOf("SF") - 2));

        /// <summary>
        /// Gets required Signal-to-noise ratio to demodulate a LoRa signal given a spread Factor
        /// Spreading Factor -> Required SNR
        /// taken from https://www.semtech.com/uploads/documents/DS_SX1276-7-8-9_W_APP_V5.pdf
        /// </summary>
        private Dictionary<int, double> SpreadFactorToSNR { get; } = new Dictionary<int, double>()
         {
            { 6,  -5 },
            { 7, -7.5 },
            { 8,  -10 },
            { 9, -12.5 },
            { 10, -15 },
            { 11, -17.5 },
            { 12, -20 }
        };

        [JsonExtensionData]
        public Dictionary<string, object> ExtraData { get; } = new Dictionary<string, object>();

        /// <summary>
        /// Method to create a Rxpk object from a byte array.
        /// This is typically used for an upstream communication.
        /// </summary>
        /// <param name="inputMessage">Input byte array</param>
        /// <returns>List of rxpk or null if no Rxpk was found</returns>
        public static List<Rxpk> CreateRxpk(byte[] inputMessage)
        {
            PhysicalPayload physicalPayload = new PhysicalPayload(inputMessage);
            if (physicalPayload.Message != null)
            {
                var payload = Encoding.UTF8.GetString(physicalPayload.Message);
                if (!payload.StartsWith("{\"stat"))
                {
                    Logger.Log($"Physical dataUp {payload}", LogLevel.Debug);
                    var payloadObject = JsonConvert.DeserializeObject<UplinkPktFwdMessage>(payload);
                    if (payloadObject != null)
                    {
                        if (payloadObject.Rxpk != null)
                        {
                            return payloadObject.Rxpk;
                        }
                    }
                }
                else
                {
                    Logger.Log($"Statistic: {payload}", LogLevel.Debug);
                }
            }

            return new List<Rxpk>();
        }

        public uint GetModulationMargin()
        {
            // required SNR:
            var requiredSNR = this.SpreadFactorToSNR[this.SpreadingFactor];

            // get the link budget
            int signedMargin = Math.Max(0, (int)(this.Lsnr - requiredSNR));

            return (uint)signedMargin;
        }
    }
}
