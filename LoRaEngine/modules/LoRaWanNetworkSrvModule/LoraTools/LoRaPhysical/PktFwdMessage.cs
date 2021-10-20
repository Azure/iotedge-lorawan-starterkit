// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaPhysical
{
    using Newtonsoft.Json;
    using System;

    /// <summary>
    /// Base type of a Packet Forwarder message (lower level).
    /// </summary>
    public abstract class PktFwdMessage
    {
        [Obsolete("toremove")]
        [JsonIgnore]
        public abstract PktFwdMessageAdapter PktFwdMessageAdapter { get; }

        private enum PktFwdType
        {
            Downlink,
            Uplink
        }
    }
}
