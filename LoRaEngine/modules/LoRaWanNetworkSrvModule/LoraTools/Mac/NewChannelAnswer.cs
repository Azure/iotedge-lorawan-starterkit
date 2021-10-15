// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

    public class NewChannelAnswer : MacCommand
    {
        public byte Status { get; set; }

        [JsonIgnore]
        public bool DataRangeOk => ((this.Status >> 1) & 0b00000001) == 1;

        [JsonIgnore]
        public bool ChannelFreqOk => (this.Status & 0b00000001) == 1;

        public NewChannelAnswer(bool drRangeOk, bool chanFreqOk)
        {
            this.Status |= (byte)((drRangeOk ? 1 : 0) << 2);
            this.Status |= (byte)(chanFreqOk ? 1 : 0);
            this.Cid = Cid.NewChannelCmd;
        }

        public NewChannelAnswer(ReadOnlySpan<byte> input)
            : base(input)
        {
            this.Status = input[1];
            this.Cid = (Cid)input[0];
        }

        public override int Length => 2;

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.Status;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, frequency: {(this.ChannelFreqOk ? "updated" : "not updated")}, data rate: {(this.DataRangeOk ? "updated" : "not updated")}";
        }
    }
}
