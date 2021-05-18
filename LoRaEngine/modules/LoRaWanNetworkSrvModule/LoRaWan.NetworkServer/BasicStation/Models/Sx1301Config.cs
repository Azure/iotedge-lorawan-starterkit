// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Models
{
    using System.Text.Json.Serialization;

    public class Sx1301Config
    {
        [JsonPropertyName("radio_0")]
        public Radio Radio_0 { get; set; }

        [JsonPropertyName("radio_1")]
        public Radio Radio_1 { get; set; }

        [JsonPropertyName("chan_FSK")]
        public Chan_FSK Chan_FSK { get; set; }

        [JsonPropertyName("chan_Lora_std")]
        public Chan_Lora_Std Chan_Lora_std { get; set; }

        [JsonPropertyName("chan_multiSF_0")]
        public Chan_Multisf Chan_multiSF_0 { get; set; }

        [JsonPropertyName("chan_multiSF_1")]
        public Chan_Multisf Chan_multiSF_1 { get; set; }

        [JsonPropertyName("chan_multiSF_2")]
        public Chan_Multisf Chan_multiSF_2 { get; set; }

        [JsonPropertyName("chan_multiSF_3")]
        public Chan_Multisf Chan_multiSF_3 { get; set; }

        [JsonPropertyName("chan_multiSF_4")]
        public Chan_Multisf Chan_multiSF_4 { get; set; }

        [JsonPropertyName("chan_multiSF_5")]
        public Chan_Multisf Chan_multiSF_5 { get; set; }

        [JsonPropertyName("chan_multiSF_6")]
        public Chan_Multisf Chan_multiSF_6 { get; set; }

        [JsonPropertyName("chan_multiSF_7")]
        public Chan_Multisf Chan_multiSF_7 { get; set; }

        public Sx1301Config()
        {
            this.Radio_0 = new Radio
            {
                Enable = true,
                Freq = 867500000,
            };
            this.Radio_1 = new Radio
            {
                Enable = true,
                Freq = 868500000,
            };
            this.Chan_FSK = new Chan_FSK
            {
                Enable = true,
                Radio = 1,
                If = 300000,
            };
            this.Chan_Lora_std = new Chan_Lora_Std
            {
                If = -200000,
                Radio = 1,
                Enable = true,
                Bandwidth = 250000,
                Spread_factor = 7,
            };
            this.Chan_multiSF_0 = new Chan_Multisf
            {
                Enable = true,
                Radio = 1,
                If = -400000
            };
            this.Chan_multiSF_1 = new Chan_Multisf
            {
                Enable = true,
                Radio = 1,
                If = -200000
            };
            this.Chan_multiSF_2 = new Chan_Multisf
            {
                Enable = true,
                Radio = 1,
                If = 0,
            };
            this.Chan_multiSF_3 = new Chan_Multisf
            {
                Enable = true,
                Radio = 0,
                If = -400000
            };
            this.Chan_multiSF_4 = new Chan_Multisf
            {
                Enable = true,
                Radio = 0,
                If = -200000
            };
            this.Chan_multiSF_5 = new Chan_Multisf
            {
                Enable = true,
                Radio = 0,
                If = 0
            };
            this.Chan_multiSF_6 = new Chan_Multisf
            {
                Enable = true,
                Radio = 0,
                If = 200000
            };
            this.Chan_multiSF_7 = new Chan_Multisf
            {
                Enable = true,
                Radio = 0,
                If = 400000
            };
        }
    }
}
