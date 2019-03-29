// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// DevStatusAns Upstream & DevStatusReq Downstream
    /// </summary>
    public class DevStatusAnswer : MacCommand
    {
        [JsonProperty("battery")]
        public byte Battery { get; set; }

        [JsonProperty("margin")]
        private byte Margin { get; set; }

        public override int Length => 3;

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, Battery Level: {this.Battery}, Margin: {this.Margin}";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DevStatusAnswer"/> class.
        /// Upstream constructor
        /// </summary>
        public DevStatusAnswer(byte battery, byte margin)
        {
            this.Battery = battery;
            this.Margin = margin;
            this.Cid = CidEnum.DevStatusCmd;
        }

        public DevStatusAnswer(ReadOnlySpan<byte> readOnlySpan)
            : base(readOnlySpan)
        {
            if (readOnlySpan.Length < this.Length)
            {
                throw new Exception("DevStatusAnswer detected but the byte format is not correct");
            }
            else
            {
                this.Battery = readOnlySpan[1];
                this.Margin = readOnlySpan[2];
                this.Cid = (CidEnum)readOnlySpan[0];
            }
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.Margin;
            yield return (byte)this.Battery;
            yield return (byte)this.Cid;
        }
    }
}
