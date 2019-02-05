// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    public class NewChannelAnswer : MacCommand
    {
        private readonly byte status;

        public NewChannelAnswer(bool drRangeOk, bool chanFreqOk)
        {
            this.status |= (byte)((drRangeOk ? 1 : 0) << 2);
            this.status |= (byte)(chanFreqOk ? 1 : 0);
            this.Cid = CidEnum.NewChannelCmd;
        }

        public NewChannelAnswer(ReadOnlySpan<byte> readOnlySpan)
        {
            if (readOnlySpan.Length < this.Length)
            {
                throw new Exception("NewChannelAnswer detected but the byte format is not correct");
            }
            else
            {
                this.status = readOnlySpan[1];
                this.Cid = (CidEnum)readOnlySpan[0];
            }
        }

        public override int Length => 2;

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add((byte)this.status);
            return returnedBytes;
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
