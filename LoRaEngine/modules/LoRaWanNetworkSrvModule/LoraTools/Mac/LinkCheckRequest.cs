// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System.Collections.Generic;

    /// <summary>
    /// LinkCheckReq Upstream.
    /// </summary>
    public class LinkCheckRequest : MacCommand
    {
        public override int Length => 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="LinkCheckRequest"/> class.
        /// Downstream Constructor.
        /// </summary>
        public LinkCheckRequest()
        {
            Cid = Cid.LinkCheckCmd;
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
