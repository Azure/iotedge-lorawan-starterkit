// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// LinkCheckAns Downstream
    /// </summary>
    public class LinkCheckAnswer : MacCommand
    {
        public uint Margin { get; set; }

        public uint GwCnt { get; set; }

        public override int Length => 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkCheckAnswer"/> class.
        /// Upstream Constructor
        /// </summary>
        public LinkCheckAnswer(uint margin, uint gwCnt)
        {
            this.Margin = margin;
            this.GwCnt = gwCnt;
            this.Cid = CidEnum.LinkCheckCmd;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkCheckAnswer"/> class.
        /// Test Constructor
        /// </summary>
        public LinkCheckAnswer(Span<byte> input)
        {
            this.Cid = (CidEnum)input[0];
            this.Margin = (uint)input[1];
            this.GwCnt = (uint)input[2];
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add((byte)this.Margin);
            returnedBytes.Add((byte)this.GwCnt);
            return returnedBytes;
        }

        public override string ToString()
        {
            return $"Margin: {this.Margin}, gateway count : {this.GwCnt}";
        }
    }
}
