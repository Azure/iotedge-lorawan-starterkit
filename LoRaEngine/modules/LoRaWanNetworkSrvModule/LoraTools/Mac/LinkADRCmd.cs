// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;

    /// <summary>
    /// LinkAdrReq & LinkAdrAns TODO REGION SPECIFIC
    /// </summary>
    public class LinkADRCmd : GenericMACCommand
    {
        // private readonly byte dataRateTXPower;
        private readonly byte[] chMask = new byte[2];

        public LinkADRCmd()
        {
            this.Length = 2;
        }

        // private readonly byte redondancy;
        public override byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}
