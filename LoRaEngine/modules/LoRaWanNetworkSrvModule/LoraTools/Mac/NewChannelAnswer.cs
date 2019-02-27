// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Mac;

    public class NewChannelAnswer : MacCommand
    {
        public byte Status { get; set; }

        public bool GetDataRangeOk() => ((this.Status >> 1) & 0b00000001) == 1;

        public bool GetChannelFreqOk() => (this.Status & 0b00000001) == 1;

        public NewChannelAnswer(bool drRangeOk, bool chanFreqOk)
        {
            this.Status |= (byte)((drRangeOk ? 1 : 0) << 2);
            this.Status |= (byte)(chanFreqOk ? 1 : 0);
            this.Cid = CidEnum.NewChannelCmd;
        }

        public NewChannelAnswer(ReadOnlySpan<byte> readOnlySpan)
            : base(readOnlySpan)
        {
            if (readOnlySpan.Length < this.Length)
            {
                throw new MacCommandException("NewChannelAnswer detected but the byte format is not correct");
            }

            this.Status = readOnlySpan[1];
            this.Cid = (CidEnum)readOnlySpan[0];
        }

        public override int Length => 2;

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.Status;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, frequency: {(this.GetChannelFreqOk() ? "updated" : "not updated")}, data rate: {(this.GetDataRangeOk() ? "updated" : "not updated")}";
        }
    }
}
