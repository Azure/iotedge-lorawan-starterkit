// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Mac;
    using Newtonsoft.Json;

    /// <summary>
    /// RXParamSetupAns.
    /// </summary>
    public class RXParamSetupAnswer : MacCommand
    {
        [JsonProperty("status")]
        public byte Status { get; set; }

        public override int Length => 2;

        public bool Rx1DROffsetAck => ((this.Status >> 2) & 0b00000001) == 1;

        public bool Rx2DROffsetAck => ((this.Status >> 1) & 0b00000001) == 1;

        public bool ChannelAck => (this.Status & 0b00000001) == 1;

        public RXParamSetupAnswer(bool rx1DROffsetAck, bool rx2DataRateOffsetAck, bool channelAck)
        {
            this.Cid = Cid.RXParamCmd;
            this.Status |= (byte)((rx1DROffsetAck ? 1 : 0) << 2);
            this.Status |= (byte)((rx2DataRateOffsetAck ? 1 : 0) << 1);
            this.Status |= (byte)(channelAck ? 1 : 0);
        }

        public RXParamSetupAnswer(ReadOnlySpan<byte> readOnlySpan)
            : base(readOnlySpan)
        {
            if (readOnlySpan.Length < this.Length)
            {
                throw new MacCommandException("RXParamSetupAnswer detected but the byte format is not correct");
            }
            else
            {
                this.Cid = (Cid)readOnlySpan[0];
                this.Status = readOnlySpan[1];
            }
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return this.Status;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, rx1 datarate offset ack: {this.Rx1DROffsetAck}, rx2 datarate offset ack: {this.Rx2DROffsetAck}, channel ack: {this.ChannelAck}";
        }
    }
}
