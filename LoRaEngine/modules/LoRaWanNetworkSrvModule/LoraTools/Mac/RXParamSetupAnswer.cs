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

        [JsonIgnore]
        public bool Rx1DROffsetAck => ((Status >> 2) & 0b00000001) == 1;

        [JsonIgnore]
        public bool Rx2DROffsetAck => ((Status >> 1) & 0b00000001) == 1;

        [JsonIgnore]
        public bool ChannelAck => (Status & 0b00000001) == 1;

        public RXParamSetupAnswer(bool rx1DROffsetAck, bool rx2DataRateOffsetAck, bool channelAck)
        {
            Cid = Cid.RXParamCmd;
            Status |= (byte)((rx1DROffsetAck ? 1 : 0) << 2);
            Status |= (byte)((rx2DataRateOffsetAck ? 1 : 0) << 1);
            Status |= (byte)(channelAck ? 1 : 0);
        }

        public RXParamSetupAnswer(ReadOnlySpan<byte> readOnlySpan)
            : base(readOnlySpan)
        {
            if (readOnlySpan.Length < Length)
            {
                throw new MacCommandException("RXParamSetupAnswer detected but the byte format is not correct");
            }
            else
            {
                Cid = (Cid)readOnlySpan[0];
                Status = readOnlySpan[1];
            }
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return Status;
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, rx1 datarate offset ack: {Rx1DROffsetAck}, rx2 datarate offset ack: {Rx2DROffsetAck}, channel ack: {ChannelAck}";
        }
    }
}
