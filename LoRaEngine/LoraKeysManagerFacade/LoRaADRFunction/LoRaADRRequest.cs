// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using LoRaWan;

    public class LoRaADRRequest
    {
        public DataRate DataRate { get; set; }

        public float RequiredSnr { get; set; }

        public bool PerformADRCalculation { get; set; }

        public uint FCntUp { get; set; }

        public uint FCntDown { get; set; }

        public int MinTxPowerIndex { get; set; }

        public string GatewayId { get; set; }

        public bool ClearCache { get; set; }

        public DataRate MaxDataRate { get; internal set; }
    }
}
