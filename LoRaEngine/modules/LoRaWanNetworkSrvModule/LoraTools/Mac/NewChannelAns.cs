// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;

    public class NewChannelAns : NewChannelCmd
    {
        private readonly bool drRangOk;

        private readonly bool chanFreqOk;

        public NewChannelAns(bool drRangeOk, bool chanFreqOk)
        {
            this.Length = 2;
            this.drRangOk = drRangeOk;
            this.chanFreqOk = chanFreqOk;
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
