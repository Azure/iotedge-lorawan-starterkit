// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public class UpstreamDataFrame
    {
        public UpstreamDataFrame(MacHeader mHdr, DevAddr devAddr, FrameControl frameControl, ushort frameCounter, string fOpts, FramePort fPort, string fRMPayload, Mic mic)
        {
            MHdr = mHdr;
            DevAddr = devAddr;
            FrameControl = frameControl;
            FrameCounter = frameCounter;
            FOpts = fOpts;
            FPort = fPort;
            FRMPayload = fRMPayload;
            Mic = mic;
        }

        public MacHeader MHdr { get; }
        public DevAddr DevAddr { get; }
        public FrameControl FrameControl { get; }
        public ushort FrameCounter { get; }
        public string FOpts { get; }
        public FramePort FPort { get; }
        public string FRMPayload { get; }
        public Mic Mic { get; }
        public RadioMetadata RadioMetadata { get; private set; }

        public void SetRadioMetadata(RadioMetadata radioMetadata)
        {
            RadioMetadata = radioMetadata;
        }
    }
}
