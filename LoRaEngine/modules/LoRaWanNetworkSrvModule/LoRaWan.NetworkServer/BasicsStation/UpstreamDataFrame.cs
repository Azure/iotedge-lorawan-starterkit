// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public class UpstreamDataFrame
    {
        public UpstreamDataFrame(MacHeader macHeader,
                                 DevAddr devAddress,
                                 FCtrlFlags fctrlFlags,
                                 ushort counter,
                                 string options,
                                 FramePort port,
                                 string payload,
                                 Mic mic,
                                 RadioMetadata radioMetadata)
        {
            MacHeader = macHeader;
            DevAddr = devAddress;
            FrameControlFlags = fctrlFlags;
            Counter = counter;
            Options = options;
            Port = port;
            Payload = payload;
            Mic = mic;
            RadioMetadata = radioMetadata;
        }

        public MacHeader MacHeader { get; }
        public DevAddr DevAddr { get; }
        public FCtrlFlags FrameControlFlags { get; }
        public ushort Counter { get; }
        public string Options { get; }
        public FramePort Port { get; }
        public string Payload { get; }
        public Mic Mic { get; }
        public RadioMetadata RadioMetadata { get; }
    }
}
