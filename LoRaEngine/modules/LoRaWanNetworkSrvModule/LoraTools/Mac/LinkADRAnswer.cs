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
    public class LinkADRAnswer : MacCommand
    {
        [JsonProperty("status")]
        public byte Status { get; set; }

        public override int Length => 2;

        [JsonIgnore]
        public bool PowerAck => ((Status >> 2) & 0b00000001) == 1;

        [JsonIgnore]
        public bool DRAck => ((Status >> 1) & 0b00000001) == 1;

        [JsonIgnore]
        public bool CHMaskAck => ((Status >> 0) & 0b00000001) == 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRAnswer"/> class.
        /// </summary>
        public LinkADRAnswer(byte powerAck, bool dataRateAck, bool channelMaskAck)
        {
            Cid = Cid.LinkADRCmd;
            Status |= (byte)((byte)(powerAck & 0b00000011) << 2);
            Status |= (byte)((byte)(dataRateAck ? 1 << 1 : 0 << 1) | (byte)(channelMaskAck ? 1 : 0));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRAnswer"/> class.
        /// </summary>
        public LinkADRAnswer(ReadOnlySpan<byte> readOnlySpan)
            : base(readOnlySpan)
        {
            Cid = (Cid)readOnlySpan[0];
            Status = readOnlySpan[1];
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return Status;
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, power: {(PowerAck ? "changed" : "not changed")}, data rate: {(DRAck ? "changed" : "not changed")}, channels: {(CHMaskAck ? "changed" : "not changed")}";
        }
    }
}
