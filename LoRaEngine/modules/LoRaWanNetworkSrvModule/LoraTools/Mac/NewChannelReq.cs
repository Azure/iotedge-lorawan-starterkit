// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;

    /// <summary>
    /// Both ways
    /// </summary>
    public class NewChannelReq : NewChannelCmd
    {
        private readonly uint chIndex;

        private readonly uint freq;

        private readonly uint maxDR;

        private readonly uint minDR;

        public NewChannelReq(uint chIndex, uint freq, uint maxDr, uint minDr)
        {
            this.Length = 4;
            this.chIndex = chIndex;
            this.freq = freq;
            this.maxDR = maxDr;
            this.minDR = minDr;
            this.Cid = CidEnum.NewChannelCmd;
        }

        public override byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}
