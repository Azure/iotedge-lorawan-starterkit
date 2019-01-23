// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    /// <summary>
    /// DevStatusAns Upstream & DevStatusReq Downstream
    /// </summary>
    public class DevStatusCmd : GenericMACCommand
    {
        public uint Battery { get; set; }

        public int Margin { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DevStatusCmd"/> class.
        /// Upstream Constructor
        /// </summary>
        public DevStatusCmd()
        {
            this.Length = 1;
            this.Cid = CidEnum.DevStatusCmd;
        }

        public override string ToString()
        {
            return string.Format("Battery Level : {0}, Margin : {1}", this.Battery, this.Margin);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DevStatusCmd"/> class.
        /// Upstream constructor
        /// </summary>
        public DevStatusCmd(uint battery, int margin)
        {
            this.Length = 3;
            this.Battery = battery;
            this.Margin = margin;
            this.Cid = CidEnum.DevStatusCmd;
        }

        public override byte[] ToBytes()
        {
            byte[] returnedBytes = new byte[this.Length];
            returnedBytes[0] = (byte)this.Cid;
            return returnedBytes;
        }
    }
}
