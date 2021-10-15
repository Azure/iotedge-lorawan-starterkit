// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// LinkAdrRequest Downstream.
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

        public int TxPower => this.DataRateTXPower & 0b00001111;

        public int ChMaskCntl => (this.Redundancy >> 4) & 0b00000111;

        public int NbRep => this.Redundancy & 0b00001111;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRRequest"/> class.
        /// </summary>
        public LinkADRRequest(byte datarate, byte txPower, ushort chMask, byte chMaskCntl, byte nbTrans)
        {
            this.Cid = Cid.LinkADRCmd;
            this.DataRateTXPower = (byte)((datarate << 4) | txPower);
            this.ChMask = chMask;
            // bit 7 is RFU
            this.Redundancy = (byte)((byte)((chMaskCntl << 4) | nbTrans) & 0b01111111);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRRequest"/> class. For tests to serialize from byte.
        /// </summary>
        public LinkADRRequest(byte[] input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));

            if (input.Length < this.Length || input[0] != (byte)Cid.LinkADRCmd)
            {
                throw new Exception("the input was not in the expected form");
            }

            this.Cid = Cid.LinkADRCmd;
            this.DataRateTXPower = input[1];
            this.ChMask = BitConverter.ToUInt16(input, 2);
            this.Redundancy = input[4];
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return this.Redundancy;
            yield return (byte)(this.ChMask >> 8);
            yield return (byte)this.ChMask;
            yield return this.DataRateTXPower;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, datarate: {this.DataRate}, txpower: {this.TxPower}, nbTrans: {this.NbRep}, channel Mask Control: {this.ChMaskCntl}, Redundancy: {this.Redundancy}";
        }
    }
}
