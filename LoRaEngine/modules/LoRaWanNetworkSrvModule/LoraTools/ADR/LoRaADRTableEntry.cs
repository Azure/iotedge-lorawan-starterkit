﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    public class LoRaADRTableEntry
    {
        public string DevEUI { get; set; }

        public int FCnt { get; set; }

        public int GatewayCount { get; set; }

        public string GatewayId { get; set; }

        public float Snr { get; set; }
    }
}
