// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using System.Collections.Generic;

    public class LoRaADRTable
    {
        public const int FrameCountCaptureCount = 20;

        public int? CurrentTxPower { get; set; }

        public int? CurrentNbRep { get; set; }

        public List<LoRaADRTableEntry> Entries { get; set; } = new List<LoRaADRTableEntry>();

        public bool IsComplete => this.Entries.Count >= FrameCountCaptureCount;
    }
}
