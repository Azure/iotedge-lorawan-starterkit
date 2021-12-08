// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaPhysical
{
    using System.Text;
    using LoRaWan;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using static LoRaWan.Metric;

    public class Txpk
    {
        [JsonProperty("imme")]
        public bool Imme { get; set; }

        [JsonProperty("data")]
        public string Data { get; set; }

        [JsonProperty("tmst")]
        public long Tmst { get; set; }

        [JsonProperty("size")]
        public uint Size { get; set; }

        [JsonProperty("freq")]
        public double Freq { get; set; }
        [JsonIgnore]
        public Hertz FreqHertz { get => Mega(Freq); set => Freq = value.Mega; }

        [JsonProperty("rfch")]
        public uint Rfch { get; set; }

        [JsonProperty("modu")]
        public string Modu { get; set; }

        [JsonProperty("datr")]
        public string Datr { get; set; }

        [JsonProperty("codr")]
        public string Codr { get; set; }

        [JsonProperty("powe")]
        public uint Powe { get; set; }

        [JsonProperty("ipol")]
        public bool Ipol { get; set; }

        /// <summary>
        /// This method is used as part of Simulated device for testing purposes.
        /// </summary>
        /// <param name="inputMessage">The Input Message bytes.</param>
        public static Txpk CreateTxpk(byte[] inputMessage, ILogger logger = null)
        {
            var physicalPayload = new PhysicalPayload(inputMessage, true, logger);
            var payload = Encoding.UTF8.GetString(physicalPayload.Message);

            // deserialize for a downlink message
            // checkwith franc
            var payloadDownObject = JsonConvert.DeserializeObject<DownlinkPktFwdMessage>(payload);
            if (payloadDownObject != null)
            {
                if (payloadDownObject.Txpk != null)
                {
                    return payloadDownObject.Txpk;
                }
                else
                {
                    logger?.LogError("Error: " + payloadDownObject.Txpk);
                }
            }

            return null;
        }
    }
}
