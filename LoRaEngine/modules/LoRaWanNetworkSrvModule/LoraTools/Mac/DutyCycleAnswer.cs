// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System.Collections.Generic;

    /// <summary>
    /// DutyCycleAns Upstream.
    /// </summary>
    public class DutyCycleAnswer : MacCommand
    {
        public override int Length => 1;

        public DutyCycleAnswer()
        {
            Cid = Cid.DutyCycleCmd;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer";
        }
    }
}
