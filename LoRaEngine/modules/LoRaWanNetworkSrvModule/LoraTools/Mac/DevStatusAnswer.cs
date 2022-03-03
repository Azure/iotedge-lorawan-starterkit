// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// DevStatusAns Upstream & DevStatusReq Downstream.
    /// </summary>
    public class DevStatusAnswer : MacCommand
    {
        [JsonProperty("battery")]
        public byte Battery { get; set; }

        [JsonProperty("margin")]
        public byte Margin { get; set; }

        public override int Length => 3;

        public override string ToString()
        {
            return $"Type: {Cid} Answer, Battery Level: {Battery}, Margin: {Margin}";
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DevStatusAnswer"/> class.
        /// Upstream constructor.
        /// </summary>
        public DevStatusAnswer(byte battery, byte margin)
        {
            Battery = battery;
            Margin = margin;
            Cid = Cid.DevStatusCmd;
        }

        public DevStatusAnswer(ReadOnlySpan<byte> readOnlySpan)
            : base(readOnlySpan)
        {
            if (readOnlySpan.Length < Length)
            {
                throw new InvalidOperationException("DevStatusAnswer detected but the byte format is not correct");
            }
            else
            {
                Cid = (Cid)readOnlySpan[0];
                Battery = readOnlySpan[1];
                Margin = readOnlySpan[2];
            }
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)Cid;
            yield return Battery;
            yield return Margin;
        }
    }
}
