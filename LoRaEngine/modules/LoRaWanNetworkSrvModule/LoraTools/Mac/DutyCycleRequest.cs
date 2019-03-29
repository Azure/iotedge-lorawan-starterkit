// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    ///  DutyCycleReq Downstream
    /// </summary>
    public class DutyCycleRequest : MacCommand
    {
        [JsonProperty("dutyCyclePL")]
        public byte DutyCyclePL { get; set; }

        public override int Length => 2;

        public DutyCycleRequest()
        {
        }

        // Downstream message˙
        public DutyCycleRequest(byte dutyCyclePL)
        {
            this.Cid = CidEnum.DutyCycleCmd;
            this.DutyCyclePL = (byte)(dutyCyclePL & 0b00001111);
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.DutyCyclePL;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Request, dutyCyclePL: {this.DutyCyclePL}";
        }
    }
}
