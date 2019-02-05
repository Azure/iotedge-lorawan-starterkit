// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// DevStatusAns Upstream & DevStatusReq Downstream
    /// </summary>
    public class DevStatusAnswer : MacCommand
    {
        private byte battery;

        private byte margin;

        public override int Length => 3;

        public override string ToString()
        {
            return string.Format("Battery Level : {0}, Margin : {1}", this.battery, this.margin);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DevStatusAnswer"/> class.
        /// Upstream constructor
        /// </summary>
        public DevStatusAnswer(byte battery, byte margin)
        {
            this.battery = battery;
            this.margin = margin;
            this.Cid = CidEnum.DevStatusCmd;
        }

        public DevStatusAnswer(ReadOnlySpan<byte> readOnlySpan)
        {
            if (readOnlySpan.Length < this.Length)
            {
                throw new Exception("DevStatusAnswer detected but the byte format is not correct");
            }
            else
            {
                this.battery = readOnlySpan[1];
                this.margin = readOnlySpan[2];
                this.Cid = (CidEnum)readOnlySpan[0];
            }
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add((byte)this.battery);
            returnedBytes.Add((byte)this.margin);
            return returnedBytes;
        }
    }
}
