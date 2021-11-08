// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public class UpstreamDataFrame
    {
        public UpstreamDataFrame(MacHeader macHeader,
                                 DevAddr devAddress,
                                 FrameControl control,
                                 ushort counter,
                                 string options,
                                 FramePort port,
                                 string payload,
                                 Mic mic,
                                 RadioMetadata radioMetadata)
        {
            MacHeader = macHeader;
            DevAddr = devAddress;
            Control = control;
            Counter = counter;
            Options = options;
            Port = port;
            Payload = payload;
            Mic = mic;
            RadioMetadata = radioMetadata;
        }

        public MacHeader MacHeader { get; }
        public virtual DevAddr DevAddr { get; }
        public FrameControl Control { get; }
        public virtual ushort Counter { get; }
        public string Options { get; }
        public FramePort Port { get; }
        public virtual string Payload { get; }
        public virtual Mic Mic { get; }
        public RadioMetadata RadioMetadata { get; }
    }
}
