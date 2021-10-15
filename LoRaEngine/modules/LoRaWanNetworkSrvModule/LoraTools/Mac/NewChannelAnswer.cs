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
        public bool DataRangeOk => ((Status >> 1) & 0b00000001) == 1;

        [JsonIgnore]
        public bool ChannelFreqOk => (Status & 0b00000001) == 1;

        public NewChannelAnswer(bool drRangeOk, bool chanFreqOk)
        {
            Status |= (byte)((drRangeOk ? 1 : 0) << 2);
            Status |= (byte)(chanFreqOk ? 1 : 0);
            Cid = Cid.NewChannelCmd;
        }

        public NewChannelAnswer(ReadOnlySpan<byte> input)
            : base(input)
        {
            Status = input[1];
            Cid = (Cid)input[0];
        }

        public override int Length => 2;

        public override IEnumerable<byte> ToBytes()
        {
            yield return Status;
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, frequency: {(ChannelFreqOk ? "updated" : "not updated")}, data rate: {(DataRangeOk ? "updated" : "not updated")}";
        }
    }
}
