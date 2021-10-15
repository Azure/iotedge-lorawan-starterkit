// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    ///  DutyCycleReq Downstream.
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
            Cid = Cid.DutyCycleCmd;
            DutyCyclePL = (byte)(dutyCyclePL & 0b00001111);
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)DutyCyclePL;
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Request, dutyCyclePL: {DutyCyclePL}";
        }
    }
}
