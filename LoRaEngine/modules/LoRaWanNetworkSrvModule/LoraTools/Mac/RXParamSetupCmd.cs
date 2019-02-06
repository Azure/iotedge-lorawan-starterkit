// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;

    /// <summary>
    /// RXParamSetupReq & RXParamSetupAns TODO Region specific
    /// </summary>
    public class RXParamSetupCmd : GenericMACCommand
    {
        // private readonly byte dlSettings;
        private readonly byte[] frequency = new byte[3];

        public RXParamSetupCmd()
        {
            this.Length = 2;
        }

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
