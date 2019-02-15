// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System.Collections.Generic;

    /// <summary>
    /// DevStatusAns Upstream & DevStatusReq Downstream
    /// </summary>
    public class DevStatusRequest : MacCommand
    {
        public override int Length => 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="DevStatusRequest"/> class.
        /// Upstream Constructor
        /// </summary>
        public DevStatusRequest()
        {
            this.Cid = CidEnum.DevStatusCmd;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Request";
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)this.Cid;
        }
    }
}
