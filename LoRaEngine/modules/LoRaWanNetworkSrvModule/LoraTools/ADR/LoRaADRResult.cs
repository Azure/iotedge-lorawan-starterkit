// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.ADR
{
    using LoRaWan;

    public class LoRaADRResult
    {
        public int? NbRepetition { get; set; }

        public int? TxPower { get; set; }

        public DataRate DataRate { get; set; }

        public bool CanConfirmToDevice { get; set; }

        public uint? FCntDown { get; set; }

        public int NumberOfFrames { get; set; }
    }
}
