// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System.Collections.Generic;
    using LoRaTools.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// Both ways.
    /// </summary>
    public class NewChannelRequest : MacCommand
    {
        [JsonProperty("chIndex")]
        public byte ChIndex { get; set; }

        [JsonProperty("freq")]
        public byte[] Freq { get; set; }

        [JsonProperty("drRange")]
        public byte DrRange { get; set; }

        [JsonIgnore]
        public int MaxDR => (DrRange >> 4) & 0b00001111;

        [JsonIgnore]
        public int MinDR => DrRange & 0b00001111;

        public override int Length => 6;

        public NewChannelRequest()
        {
        }

        public NewChannelRequest(byte chIndex, byte[] freq, byte maxDr, byte minDr)
        {
            ChIndex = chIndex;
            Freq = freq;
            DrRange = (byte)((byte)(maxDr << 4) | (minDr & 0b00001111));
            Cid = Cid.NewChannelCmd;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return DrRange;
            yield return Freq[2];
            yield return Freq[1];
            yield return Freq[0];
            yield return ChIndex;
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, channel index: {ChIndex}, frequency: {ConversionHelper.ByteArrayToString(Freq)}, min DR: {MinDR}, max DR: {MaxDR}";
        }
    }
}
