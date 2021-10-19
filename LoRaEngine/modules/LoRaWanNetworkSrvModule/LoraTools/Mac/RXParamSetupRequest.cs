// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable CA1819 // Properties should not return arrays

namespace LoRaTools
{
    using System.Collections.Generic;
    using LoRaTools.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// RXParamSetupReq & RXParamSetupAns TODO Region specific.
    /// </summary>
    public class RXParamSetupRequest : MacCommand
    {
        [JsonProperty("frequency")]
        public byte[] Frequency { get; set; } = new byte[3];

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

        public RXParamSetupRequest(byte rx1DROffset, byte rx2DataRateOffset, byte[] frequency)
        {
            DlSettings = (byte)(((rx1DROffset << 4) | rx2DataRateOffset) & 0b01111111);
            Frequency = frequency;
        }

        public override IEnumerable<byte> ToBytes()
        {
            yield return Frequency[2];
            yield return Frequency[1];
            yield return Frequency[0];
            yield return DlSettings;
            yield return (byte)Cid;
        }

        public override string ToString()
        {
            return $"Type: {Cid} Answer, rx1 datarate offset: {RX1DROffset}, rx2 datarate: {RX2DataRate}, frequency plan: {ConversionHelper.ByteArrayToString(Frequency)}";
        }
    }
}
