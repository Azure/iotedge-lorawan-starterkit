// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// LinkCheckReq Upstream
    /// </summary>
    public class LinkCheckRequest : MacCommand
    {
        public override int Length => 3;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkCheckRequest"/> class.
        /// Downstream Constructor
        /// </summary>
        public LinkCheckRequest()
        {
            this.Cid = CidEnum.LinkCheckCmd;
        }

        public override IEnumerable<byte> ToBytes()
        {
            List<byte> returnedBytes = new List<byte>();
            returnedBytes.Add((byte)this.Cid);
            return returnedBytes;
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}
