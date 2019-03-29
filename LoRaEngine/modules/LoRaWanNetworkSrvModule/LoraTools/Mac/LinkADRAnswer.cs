// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// LinkAdrRequest Downstream
    /// </summary>
    public class LinkADRAnswer : MacCommand
    {
        [JsonProperty("status")]
        public byte Status { get; set; }

        public override int Length => 2;

        public bool GetPowerAck() => ((this.Status >> 2) & 0b00000001) == 1;

        public bool GetDRAck() => ((this.Status >> 1) & 0b00000001) == 1;

        public bool GetCHMaskAck() => ((this.Status >> 0) & 0b00000001) == 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRAnswer"/> class.
        /// </summary>
        public LinkADRAnswer(byte powerAck, bool dataRateAck, bool channelMaskAck)
        {
            this.Cid = CidEnum.LinkADRCmd;
            this.Status |= (byte)((byte)(powerAck & 0b00000011) << 2);
            this.Status |= (byte)((byte)(dataRateAck ? 1 << 1 : 0 << 1) | (byte)(channelMaskAck ? 1 : 0));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRAnswer"/> class.
        /// </summary>
        public LinkADRAnswer(ReadOnlySpan<byte> readOnlySpan)
            : base(readOnlySpan)
        {
            this.Cid = (CidEnum)readOnlySpan[0];
            this.Status = readOnlySpan[1];
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return this.Status;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, power: {(this.GetPowerAck() ? "changed" : "not changed")}, data rate: {(this.GetDRAck() ? "changed" : "not changed")}, channels: {(this.GetCHMaskAck() ? "changed" : "not changed")}";
        }
    }
}
