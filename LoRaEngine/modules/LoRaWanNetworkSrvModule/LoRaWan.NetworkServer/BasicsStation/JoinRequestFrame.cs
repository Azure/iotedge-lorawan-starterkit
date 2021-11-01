// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public class JoinRequestFrame
    {
        public JoinRequestFrame(MacHeader mHdr, JoinEui joinEui, DevEui devEui, DevNonce devNonce, Mic mic)
        {
            MHdr = mHdr;
            JoinEui = joinEui;
            DevEui = devEui;
            DevNonce = devNonce;
            Mic = mic;
            Mic = mic;
            MHdr = mHdr;
        }

        public RadioMetadata RadioMetadata { get; private set; }
        public MacHeader MHdr { get; }
        public JoinEui JoinEui { get; }
        public DevEui DevEui { get; }
        public DevNonce DevNonce { get; }
        public Mic Mic { get; }

        public void SetRadioMetadata(RadioMetadata radioMetadata)
        {
            RadioMetadata = radioMetadata;
        }
    }
}
