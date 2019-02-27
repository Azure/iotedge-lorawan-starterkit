// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// RXParamSetupReq & RXParamSetupAns TODO Region specific
    /// </summary>
    public class RXParamSetupRequest : MacCommand
    {
        [JsonProperty("frequency")]
        public byte[] Frequency { get; set; } = new byte[3];

        [JsonProperty("dlSettings")]
        public byte DlSettings { get; set; }

        public override int Length => 5;

        public byte GetRX1DROffset() => (byte)((this.DlSettings >> 4) & 0b00001111);

        public byte GetRX2DataRate() => (byte)(this.DlSettings & 0b00001111);

        public RXParamSetupRequest()
        {
        }

        public RXParamSetupRequest(byte rx1DROffset, byte rx2DataRateOffset, byte[] frequency)
        {
            this.DlSettings = (byte)(((rx1DROffset << 4) | rx2DataRateOffset) & 0b01111111);
            this.Frequency = frequency;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return this.Frequency[2];
            yield return this.Frequency[1];
            yield return this.Frequency[0];
            yield return this.DlSettings;
            yield return (byte)this.Cid;
        }

        public override string ToString()
        {
            return $"Type: {this.Cid} Answer, rx1 datarate offset: {this.GetRX1DROffset()}, rx2 datarate: {this.GetRX2DataRate()}, frequency plan: {ConversionHelper.ByteArrayToString(this.Frequency)}";
        }
    }
}
