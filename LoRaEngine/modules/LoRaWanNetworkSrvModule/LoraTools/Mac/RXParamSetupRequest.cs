// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// RXParamSetupReq & RXParamSetupAns TODO Region specific
    /// </summary>
    public class RXParamSetupRequest : MacCommand
    {
        // private readonly byte dlSettings;
        private readonly byte[] frequency = new byte[3];
        private readonly byte dlSettings;

        public override int Length => 5;

        public RXParamSetupRequest(byte rx1DROffset, byte rx2DataRateOffset, byte[] frequency)
        {
            this.dlSettings = (byte)(((rx1DROffset << 4) | rx2DataRateOffset) & 0b01111111);
            this.frequency = frequency;
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add(this.dlSettings);
            returnedBytes.Add(this.frequency[0]);
            returnedBytes.Add(this.frequency[1]);
            returnedBytes.Add(this.frequency[2]);
            return returnedBytes;
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
