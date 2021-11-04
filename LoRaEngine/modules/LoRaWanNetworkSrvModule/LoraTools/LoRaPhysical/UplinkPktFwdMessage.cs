// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaPhysical
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// an uplink Json for the packet forwarder.
    /// </summary>
    public class UplinkPktFwdMessage
    {
#pragma warning disable CA2227 // Collection properties should be read only
        // Class is a DTO.
        public IList<Rxpk> Rxpk { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only

        public UplinkPktFwdMessage()
        {
            Rxpk = new List<Rxpk>();
        }

        public UplinkPktFwdMessage(Rxpk rxpkInput)
        {
            Rxpk = new List<Rxpk>()
            {
                rxpkInput
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UplinkPktFwdMessage"/> class.
        /// This method is used in case of a request to a upstream message.
        /// </summary>
        /// <param name="LoraMessage">the serialized LoRa Message.</param>
        /// <returns>UplinkPktFwdMessage object ready to be sent.</returns>
        public UplinkPktFwdMessage(byte[] loRaData, string datr, double freq, uint tmst = 0, float lsnr = 0)
        {
            if (loRaData is null) throw new ArgumentNullException(nameof(loRaData));

            // This is a new ctor, must be validated by MIK
            Rxpk = new List<Rxpk>()
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
    }
}
