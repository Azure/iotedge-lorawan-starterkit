// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// RXTimingSetupAns Upstream & RXTimingSetupReq Downstream.
    /// </summary>
    public class RXTimingSetupRequest : MacCommand
    {
        [JsonProperty("settings")]
        public byte Settings { get; set; }

        public override int Length => 2;

        public int Delay => this.Settings & 0b00001111;

        public RXTimingSetupRequest()
        {
        }

        public RXTimingSetupRequest(byte delay)
        {
            this.Cid = Cid.RXTimingCmd;
            this.Settings |= delay;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.Settings;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, delay: {this.Delay}";
        }
    }
}
