// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// LinkCheckAns Downstream.
    /// </summary>
    public class LinkCheckAnswer : MacCommand
    {
        [JsonProperty("margin")]
        public byte Margin { get; set; }

        [JsonProperty("gwCnt")]
        public byte GwCnt { get; set; }

        public override int Length => 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkCheckAnswer"/> class.
        /// Upstream Constructor.
        /// </summary>
        public LinkCheckAnswer(byte margin, byte gwCnt)
        {
            Margin = margin;
            GwCnt = gwCnt;
            Cid = Cid.LinkCheckCmd;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkCheckAnswer"/> class.
        /// </summary>
        public LinkCheckAnswer(ReadOnlySpan<byte> input)
        {
            Cid = (Cid)input[0];
            Margin = input[1];
            GwCnt = input[2];
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)Cid;
            yield return Margin;
            yield return GwCnt;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, margin: {Margin}, gateway count: {GwCnt}";
        }
    }
}
