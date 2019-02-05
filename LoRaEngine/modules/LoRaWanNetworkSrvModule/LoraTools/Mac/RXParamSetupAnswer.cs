// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// RXParamSetupAns
    /// </summary>
    public class RXParamSetupAnswer : MacCommand
    {
        private readonly byte status;

        public override int Length => 2;

        public RXParamSetupAnswer(bool rx1DROffsetAck, bool rx2DataRateOffsetAck, bool channelAck)
        {
            this.Cid = CidEnum.RXParamCmd;
            this.status |= (byte)((rx1DROffsetAck ? 1 : 0) << 2);
            this.status |= (byte)((rx2DataRateOffsetAck ? 1 : 0) << 1);
            this.status |= (byte)(channelAck ? 1 : 0);
        }

        public RXParamSetupAnswer(ReadOnlySpan<byte> readOnlySpan)
        {
            if (readOnlySpan.Length < this.Length)
            {
                throw new Exception("RXParamSetupAnswer detected but the byte format is not correct");
            }
            else
            {
                this.Cid = (CidEnum)readOnlySpan[0];
                this.status = readOnlySpan[1];
            }
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
