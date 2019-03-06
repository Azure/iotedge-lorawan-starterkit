// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// Both ways
    /// </summary>
    public class NewChannelRequest : MacCommand
    {
        [JsonProperty("chIndex")]
        public byte ChIndex { get; set; }

        [JsonProperty("freq")]
        public byte[] Freq { get; set; }

        [JsonProperty("drRange")]
        public byte DrRange { get; set; }

        public int GetMaxDR() => (this.DrRange >> 4) & 0b00001111;

        public int GetMinDR() => this.DrRange & 0b00001111;

        public override int Length => 6;

        public NewChannelRequest()
        {
        }

        public NewChannelRequest(byte chIndex, byte[] freq, byte maxDr, byte minDr)
        {
            this.ChIndex = chIndex;
            this.Freq = freq;
            this.DrRange = (byte)((byte)(maxDr << 4) | (minDr & 0b00001111));
            this.Cid = CidEnum.NewChannelCmd;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.DrRange;
            yield return (byte)this.Freq[2];
            yield return (byte)this.Freq[1];
            yield return (byte)this.Freq[0];
            yield return (byte)this.ChIndex;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, channel index: {this.ChIndex}, frequency: {ConversionHelper.ByteArrayToString(this.Freq)}, min DR: {this.GetMinDR()}, max DR: {this.GetMaxDR()}";
        }
    }
}
