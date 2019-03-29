// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// LinkCheckAns Downstream
    /// </summary>
    public class LinkCheckAnswer : MacCommand
    {
        [JsonProperty("margin")]
        public uint Margin { get; set; }

        [JsonProperty("gwCnt")]
        public uint GwCnt { get; set; }

        public override int Length => 3;

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
        public LinkCheckAnswer(ReadOnlySpan<byte> input)
        {
            this.Cid = (CidEnum)input[2];
            this.Margin = (uint)input[1];
            this.GwCnt = (uint)input[0];
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.GwCnt;
            yield return (byte)this.Margin;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, margin: {this.Margin}, gateway count: {this.GwCnt}";
        }
    }
}
