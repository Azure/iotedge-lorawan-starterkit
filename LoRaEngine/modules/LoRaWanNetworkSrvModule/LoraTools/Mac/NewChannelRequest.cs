// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Both ways
    /// </summary>
    public class NewChannelRequest : MacCommand
    {
        private readonly byte chIndex;

        private readonly byte[] freq;

        private readonly byte drRange;

        public override int Length => 6;

        public NewChannelRequest(byte chIndex, byte[] freq, byte maxDr, byte minDr)
        {
            this.chIndex = chIndex;
            this.freq = freq;
            this.drRange = (byte)((byte)(maxDr << 4) | (minDr & 0b00001111));
            this.Cid = CidEnum.NewChannelCmd;
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add((byte)this.chIndex);
            returnedBytes.Add((byte)this.freq[0]);
            returnedBytes.Add((byte)this.freq[1]);
            returnedBytes.Add((byte)this.freq[2]);
            returnedBytes.Add((byte)this.drRange);
            return returnedBytes;
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
