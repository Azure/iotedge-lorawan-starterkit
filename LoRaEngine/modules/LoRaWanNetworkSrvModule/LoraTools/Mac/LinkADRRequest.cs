// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaWan;
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

        public DataRateIndex DataRate => (DataRateIndex)((DataRateTXPower >> 4) & 0b00001111);

        public int TxPower => DataRateTXPower & 0b00001111;

        public int ChMaskCntl => (Redundancy >> 4) & 0b00000111;

        public int NbRep => Redundancy & 0b00001111;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRRequest"/> class.
        /// </summary>
        public LinkADRRequest(ushort datarate, ushort txPower, ushort chMask, ushort chMaskCntl, ushort nbTrans)
        {
            Cid = Cid.LinkADRCmd;
            DataRateTXPower = (byte)((datarate << 4) | txPower);
            ChMask = chMask;
            // bit 7 is RFU
            Redundancy = (byte)((byte)((chMaskCntl << 4) | nbTrans) & 0b01111111);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRRequest"/> class. For tests to serialize from byte.
        /// </summary>
        public LinkADRRequest(byte[] input)
        {
            if (input is null) throw new ArgumentNullException(nameof(input));

            if (input.Length < Length || input[0] != (byte)Cid.LinkADRCmd)
            {
                throw new ArgumentException("the input was not in the expected form");
            }

            Cid = Cid.LinkADRCmd;
            DataRateTXPower = input[1];
            ChMask = BitConverter.ToUInt16(input, 2);
            Redundancy = input[4];
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return Redundancy;
            yield return (byte)(ChMask >> 8);
            yield return (byte)ChMask;
            yield return DataRateTXPower;
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, datarate: {(int)DataRate}, txpower: {TxPower}, nbTrans: {NbRep}, channel Mask Control: {ChMaskCntl}, Redundancy: {Redundancy}";
        }
    }
}
