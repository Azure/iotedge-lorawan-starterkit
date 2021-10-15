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

        [JsonIgnore]
        public int Delay => Settings & 0b00001111;

        public RXTimingSetupRequest()
        {
        }

        public RXTimingSetupRequest(byte delay)
        {
            Cid = Cid.RXTimingCmd;
            Settings |= delay;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return Settings;
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, delay: {Delay}";
        }
    }
}
