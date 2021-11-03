// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public class UpstreamDataFrame
    {
        public UpstreamDataFrame(DevAddr devAddr, ushort frameCounter, string fRMPayload, Mic mic)
        {
            DevAddr = devAddr;
            FrameCounter = frameCounter;
            FRMPayload = fRMPayload;
            Mic = mic;
        }

        public DevAddr DevAddr { get; }
        public ushort FrameCounter { get; }
        public string FRMPayload { get; }
        public Mic Mic { get; }
    }
}
