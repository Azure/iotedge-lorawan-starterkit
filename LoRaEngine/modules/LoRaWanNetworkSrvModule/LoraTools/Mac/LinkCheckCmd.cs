// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;

    /// <summary>
    /// LinkCheckReq Upstream & LinkCheckAns Downstream
    /// </summary>
    public class LinkCheckCmd : GenericMACCommand
    {
        uint Margin { get; set; }

        uint GwCnt { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkCheckCmd"/> class.
        /// Upstream Constructor
        /// </summary>
        public LinkCheckCmd()
        {
            this.Length = 1;
            this.Cid = CidEnum.LinkCheckCmd;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkCheckCmd"/> class.
        /// Downstream Constructor
        /// </summary>
        public LinkCheckCmd(uint margin, uint gwCnt)
        {
            this.Length = 3;
            this.Cid = CidEnum.LinkCheckCmd;
            this.Margin = margin;
            this.GwCnt = gwCnt;
        }

        public override byte[] ToBytes()
        {
            byte[] returnedBytes = new byte[this.Length];
            returnedBytes[0] = (byte)this.Cid;
            returnedBytes[1] = BitConverter.GetBytes(this.Margin)[0];
            returnedBytes[2] = BitConverter.GetBytes(this.GwCnt)[0];
            return returnedBytes;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}
