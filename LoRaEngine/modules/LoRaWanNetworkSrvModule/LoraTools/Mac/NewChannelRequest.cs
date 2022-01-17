// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1819 // Properties should not return arrays

namespace LoRaTools
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Diagnostics;
    using LoRaWan;
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
            Debug.Assert(Freq.Length == 3);
            Span<byte> bytes = stackalloc byte[Freq.Length + 1];
            Freq.CopyTo(bytes);
            var freq = BinaryPrimitives.ReadInt32LittleEndian(bytes);

            return $"Type: {Cid} Answer, channel index: {ChIndex}, frequency: {freq:X6}, min DR: {MinDR}, max DR: {MaxDR}";
        }
    }
}
