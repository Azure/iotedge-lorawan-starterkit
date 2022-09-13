// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    internal class Beaconing
    {

        public Beaconing(uint dR, uint[] layout, uint[] freqs)
        {
            DR = dR;
            this.layout = layout;
            this.freqs = freqs;
        }

        public uint DR { get; set; }
        public uint[] layout { get; set; }
        public uint[] freqs { get; set; }
    }
}
