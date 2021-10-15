// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaPhysical
{
    using System;
    using System.Collections.Generic;

    [Obsolete("This class will be faded out in the next versions, please use DownlinkPktFwdMessage or UplinkPktFwdMessage instead.")]
    public class PktFwdMessageAdapter
    {
        public PktFwdMessageAdapter() { }

        public PktFwdMessageAdapter(IList<Rxpk> rxpks, Txpk txpk)
        {
            Rxpks = rxpks;
            Txpk = txpk;
        }

        public IList<Rxpk> Rxpks { get; } = new List<Rxpk>();

        public Txpk Txpk { get; set; }
    }
}
