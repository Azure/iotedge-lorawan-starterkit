// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaPhysical
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// an uplink Json for the packet forwarder.
    /// </summary>
    public class UplinkPktFwdMessage : PktFwdMessage
    {
        public List<Rxpk> Rxpk { get; set; }

        public UplinkPktFwdMessage()
        {
            this.Rxpk = new List<Rxpk>();
        }

        public UplinkPktFwdMessage(Rxpk rxpkInput)
        {
            this.Rxpk = new List<Rxpk>()
            {
                rxpkInput
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UplinkPktFwdMessage"/> class.
        /// This method is used in case of a request to a upstream message.
        /// </summary>
        /// <param name="LoraMessage">the serialized LoRa Message.</param>
        /// <returns>UplinkPktFwdMessage object ready to be sent</returns>
        public UplinkPktFwdMessage(byte[] loRaData, string datr, double freq, uint tmst = 0, float lsnr = 0)
        {
            // This is a new ctor, must be validated by MIK
            this.Rxpk = new List<Rxpk>()
            {
                new Rxpk()
                {
                    Tmst = tmst,
                    Data = Convert.ToBase64String(loRaData),
                    Size = (uint)loRaData.Length,
                    Freq = freq,
                    // TODO check this,
                    Rfch = 1,
                    Modu = "LORA",
                    Datr = datr,
                    Codr = "4/5",
                    Lsnr = lsnr
                }
            };
        }

        [Obsolete("to remove")]
        public override PktFwdMessageAdapter GetPktFwdMessage()
        {
            PktFwdMessageAdapter pktFwdMessageAdapter = new PktFwdMessageAdapter
            {
                Rxpks = this.Rxpk
            };
            return pktFwdMessageAdapter;
        }
    }
}