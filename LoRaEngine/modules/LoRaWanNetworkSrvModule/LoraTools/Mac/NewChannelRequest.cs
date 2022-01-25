// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// Both ways.
    /// </summary>
    public class NewChannelRequest : MacCommand
    {
        private int freq;

        [JsonProperty("chIndex")]
        public byte ChIndex { get; set; }


        [JsonProperty("freq")]
        public int Freq
        {
            get => this.freq;
            set => this.freq = value is >= 0 and <= 16_777_215
                             ? value
                             : throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }

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

        public NewChannelRequest(byte chIndex, int freq, byte maxDr, byte minDr)
        {
            ChIndex = chIndex;
            Freq = freq;
            DrRange = (byte)((byte)(maxDr << 4) | (minDr & 0b00001111));
            Cid = Cid.NewChannelCmd;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)Cid;
            yield return ChIndex;
            var freq = Freq;
            unchecked
            {
                yield return (byte)freq;
                yield return (byte)(freq >> 8);
                yield return (byte)(freq >> 16);
            }
            yield return DrRange;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, channel index: {ChIndex}, frequency: {Freq}, min DR: {MinDR}, max DR: {MaxDR}";
        }
    }
}
