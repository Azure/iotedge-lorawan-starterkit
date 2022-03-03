// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1819 // Properties should not return arrays

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    /// <summary>
    /// RXParamSetupReq & RXParamSetupAns TODO Region specific.
    /// </summary>
    public class RXParamSetupRequest : MacCommand
    {
        private int frequency;

        [JsonProperty("frequency")]
        public int Frequency
        {
            get => this.frequency;
            set => this.frequency = value is >= 0 and <= 16_777_215
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }

        [JsonProperty("dlSettings")]
        public byte DlSettings { get; set; }

        public override int Length => 5;

        [JsonIgnore]
        public byte RX1DROffset => (byte)((DlSettings >> 4) & 0b00001111);

        [JsonIgnore]
        public byte RX2DataRate => (byte)(DlSettings & 0b00001111);

        public RXParamSetupRequest()
        {
        }

        public RXParamSetupRequest(byte rx1DROffset, byte rx2DataRateOffset, int frequency)
        {
            Cid = Cid.RXParamCmd;
            DlSettings = (byte)(((rx1DROffset << 4) | rx2DataRateOffset) & 0b01111111);
            Frequency = frequency;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return (byte)Cid;
            yield return DlSettings;
            var freq = Frequency;
            unchecked
            {
                yield return (byte)freq;
                yield return (byte)(freq >> 8);
                yield return (byte)(freq >> 16);
            }
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, rx1 datarate offset: {RX1DROffset}, rx2 datarate: {RX2DataRate}, frequency plan: {Frequency}";
        }
    }
}
