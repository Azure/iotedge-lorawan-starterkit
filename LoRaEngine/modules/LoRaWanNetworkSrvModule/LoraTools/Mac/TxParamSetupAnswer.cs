// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Mac
{
    using System.Collections.Generic;

    internal class TxParamSetupAnswer : MacCommand
    {
        public TxParamSetupAnswer()
        {
            Cid = Cid.TxParamSetupCmd;
        }

        public override int Length => 1;

        public override IEnumerable<byte> ToBytes() =>
            new byte[] { (byte)Cid };

        public override string ToString() =>
            $"Type: {Cid} Answer";
    }
}
