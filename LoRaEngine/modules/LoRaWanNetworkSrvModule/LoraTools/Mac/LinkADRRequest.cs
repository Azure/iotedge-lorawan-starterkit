﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// LinkAdrRequest Downstream
    /// </summary>
    public class LinkADRRequest : MacCommand
    {
        [JsonProperty("dataRateTXPower")]
        public byte DataRateTXPower { get; set; }

        [JsonProperty("chMask")]
        public ushort ChMask { get; set; }

        [JsonProperty("redundancy")]
        public byte Redundancy { get; set; }

        public override int Length => 5;

        public int DataRate => (this.DataRateTXPower >> 4) & 0b00001111;

        public int GetTXPower() => this.DataRateTXPower & 0b00001111;

        public int GetChMaskCntl => (this.ChMask >> 4) & 0b00000111;

        public int GetNbTrans() => this.ChMask & 0b00001111;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRRequest"/> class.
        /// </summary>
        public LinkADRRequest(byte datarate, byte txPower, ushort chMask, byte chMaskCntl, byte nbTrans)
        {
            this.Cid = CidEnum.LinkADRCmd;
            this.DataRateTXPower = (byte)((datarate << 4) | txPower);
            this.ChMask = chMask;
            // bit 7 is RFU
            this.Redundancy = (byte)((byte)((chMaskCntl << 4) | nbTrans) & 0b01111111);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRRequest"/> class.
        /// </summary>
        public LinkADRRequest(IDictionary<string, string> dictionary)
        {
            if (dictionary.TryGetValueCaseInsensitive("datarate", out string dataratestr) &&
                dictionary.TryGetValueCaseInsensitive("txpower", out string txpowerstr) &&
                dictionary.TryGetValueCaseInsensitive("chMask", out string chMaskstr) &&
                dictionary.TryGetValueCaseInsensitive("chMaskCntl", out string chMaskCntlstr) &&
                dictionary.TryGetValueCaseInsensitive("nbTrans", out string nbTransstr))
            {
                if (byte.TryParse(dataratestr, out byte datarate) &&
                    byte.TryParse(txpowerstr, out byte txPower) &&
                    ushort.TryParse(chMaskstr, out ushort chMask) &&
                    byte.TryParse(chMaskCntlstr, out byte chMaskCntl) &&
                    byte.TryParse(nbTransstr, out byte nbTrans))
                {
                    this.Cid = CidEnum.LinkADRCmd;
                    this.DataRateTXPower = (byte)((datarate << 4) | txPower);
                    this.ChMask = chMask;
                    // bit 7 is RFU
                    this.Redundancy = (byte)((byte)((chMaskCntl << 4) | nbTrans) & 0b01111111);
                }
                else
                {
                    throw new Exception("LinkADRRequest C2D properties must be in String Integer style");
                }
            }
            else
            {
                throw new Exception("LinkADRRequest C2D must have have the following message properties set : datarate, txpower, chMask, chMaskCntl, nbTrans");
            }
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.Cid;
            yield return this.DataRateTXPower;
            yield return (byte)this.ChMask;
            yield return (byte)(this.ChMask >> 8);
            yield return this.Redundancy;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, datarate: {this.DataRate}, txpower: {this.GetTXPower()}, nbTrans: {this.GetNbTrans()}, channel Mask Control: {this.GetChMaskCntl}, Redundancy: {this.Redundancy}";
        }
    }
}
