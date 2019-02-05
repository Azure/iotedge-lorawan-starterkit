// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// RXTimingSetupAns Upstream & RXTimingSetupReq Downstream
    /// </summary>
    public class RXTimingSetupRequest : MacCommand
    {
        private readonly byte settings;

        public override int Length => 2;

        public RXTimingSetupRequest(byte delay)
        {
            this.Cid = CidEnum.RXTimingCmd;
            this.settings |= delay;
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            returnedBytes.Add((byte)this.settings);
            return returnedBytes;
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
