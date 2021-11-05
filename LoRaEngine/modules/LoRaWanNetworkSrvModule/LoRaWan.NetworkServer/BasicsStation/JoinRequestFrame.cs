// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public class JoinRequestFrame
    {
        public JoinRequestFrame(MacHeader mHdr, JoinEui joinEui, DevEui devEui, DevNonce devNonce, Mic mic, RadioMetadata radioMetadata)
        {
            MacHeader = mHdr;
            JoinEui = joinEui;
            DevEui = devEui;
            DevNonce = devNonce;
            Mic = mic;
            RadioMetadata = radioMetadata;
        }

        public RadioMetadata RadioMetadata { get; }
        public MacHeader MacHeader { get; }
        public JoinEui JoinEui { get; }
        public DevEui DevEui { get; }
        public DevNonce DevNonce { get; }
        public Mic Mic { get; }
    }
}
