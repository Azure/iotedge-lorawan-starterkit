// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// LinkAdrRequest Downstream
    /// </summary>
    public class LinkADRAnswer : MacCommand
    {
        private readonly byte status;

        public override int Length => 2;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRAnswer"/> class.
        /// </summary>
        public LinkADRAnswer(byte powerAck, bool dataRateAck, bool channelMaskAck)
        {
            this.Cid = CidEnum.LinkADRCmd;
            this.status |= (byte)((byte)(powerAck & 0b00000011) << 2);
            this.status |= (byte)((byte)(dataRateAck ? 1 << 1 : 0 << 1) | (byte)(channelMaskAck ? 1 : 0));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkADRAnswer"/> class.
        /// </summary>
        public LinkADRAnswer(ReadOnlySpan<byte> readOnlySpan)
        {
            this.Cid = (CidEnum)readOnlySpan[0];
            this.status = readOnlySpan[1];
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add(this.status);
            return returnedBytes;
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
