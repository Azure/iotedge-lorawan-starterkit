// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///  DutyCycleReq Downstream
    /// </summary>
    public class DutyCycleRequest : MacCommand
    {
        private readonly byte dutyCyclePL;

        public override int Length => 2;

        // Downstream message
        public DutyCycleRequest(byte dutyCyclePL)
        {
            this.Cid = CidEnum.DutyCycleCmd;
            this.dutyCyclePL = (byte)(dutyCyclePL & 0b00001111);
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add((byte)this.dutyCyclePL);
            return returnedBytes;
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
