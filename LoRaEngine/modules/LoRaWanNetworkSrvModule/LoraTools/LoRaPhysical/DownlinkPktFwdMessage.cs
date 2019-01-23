// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaPhysical
{
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// JSON of a Downlink message for the Packet forwarder.
    /// </summary>
    public class DownlinkPktFwdMessage : PktFwdMessage
    {
        [JsonProperty("txpk")]
        public Txpk Txpk { get; set; }

        public DownlinkPktFwdMessage()
        {
        }

        [Obsolete("This constructor will be faded out at message processor refactory")]
        public DownlinkPktFwdMessage(string data, string datr = "SF12BW125", uint rfch = 0, double freq = 869.525000, long tmst = 0)
        {
            var byteData = Convert.FromBase64String(data);
            this.Txpk = new Txpk()
            {
                Imme = tmst == 0 ? true : false,
                Tmst = tmst,
                Data = data,
                Size = (uint)byteData.Length,
                Freq = freq,
                Rfch = rfch,
                Modu = "LORA",
                Datr = datr,
                Codr = "4/5",
                // TODO put 14 for EU
                Powe = 14,
                Ipol = true
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DownlinkPktFwdMessage"/> class.
        /// This method is used in case of a response to a upstream message.
        /// </summary>
        /// <returns>DownlinkPktFwdMessage object ready to be sent</returns>
        public DownlinkPktFwdMessage(byte[] loRaData, string datr, double freq, long tmst = 0)
        {
            this.Txpk = new Txpk()
            {
                Imme = tmst == 0 ? true : false,
                Tmst = tmst,
                Data = Convert.ToBase64String(loRaData),
                Size = (uint)loRaData.Length,
                Freq = freq,
                Rfch = 0,
                Modu = "LORA",
                Datr = datr,
                Codr = "4/5",
                // TODO put 14 for EU
                Powe = 14,
                Ipol = true
            };
        }

        [Obsolete("ad")]
        public override PktFwdMessageAdapter GetPktFwdMessage()
        {
            PktFwdMessageAdapter pktFwdMessageAdapter = new PktFwdMessageAdapter
            {
                Txpk = this.Txpk
            };
            return pktFwdMessageAdapter;
        }
    }
}