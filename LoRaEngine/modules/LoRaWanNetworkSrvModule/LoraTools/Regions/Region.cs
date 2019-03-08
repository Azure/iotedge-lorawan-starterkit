﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public class Region
    {
        static readonly Region EU868;
        static readonly Region US915;

        static Region()
        {
            EU868 = new Region(
                LoRaRegion.EU868,
                0x34,
                ConversionHelper.StringToByteArray("C194C1"),
                (frequency: 869.525, datr: 0),
                1,
                2,
                5,
                6,
                16384,
                64,
                32,
                (min: 1, max: 3));
            EU868.DRtoConfiguration.Add(0, (configuration: "SF12BW125", maxPyldSize: 59));
            EU868.DRtoConfiguration.Add(1, (configuration: "SF11BW125", maxPyldSize: 59));
            EU868.DRtoConfiguration.Add(2, (configuration: "SF10BW125", maxPyldSize: 59));
            EU868.DRtoConfiguration.Add(3, (configuration: "SF9BW125", maxPyldSize: 123));
            EU868.DRtoConfiguration.Add(4, (configuration: "SF8BW125", maxPyldSize: 230));
            EU868.DRtoConfiguration.Add(5, (configuration: "SF7BW125", maxPyldSize: 230));
            EU868.DRtoConfiguration.Add(6, (configuration: "SF7BW250", maxPyldSize: 230));
            EU868.DRtoConfiguration.Add(7, (configuration: "50", maxPyldSize: 230)); // USED FOR GFSK

            EU868.TXPowertoMaxEIRP.Add(0, 16);
            EU868.TXPowertoMaxEIRP.Add(1, 14);
            EU868.TXPowertoMaxEIRP.Add(2, 12);
            EU868.TXPowertoMaxEIRP.Add(3, 10);
            EU868.TXPowertoMaxEIRP.Add(4, 8);
            EU868.TXPowertoMaxEIRP.Add(5, 6);
            EU868.TXPowertoMaxEIRP.Add(6, 4);
            EU868.TXPowertoMaxEIRP.Add(7, 2);

            EU868.RX1DROffsetTable = new int[8, 6]
            {
            { 0, 0, 0, 0, 0, 0 },
            { 1, 0, 0, 0, 0, 0 },
            { 2, 1, 0, 0, 0, 0 },
            { 3, 2, 1, 0, 0, 0 },
            { 4, 3, 2, 1, 0, 0 },
            { 5, 4, 3, 2, 1, 0 },
            { 6, 5, 4, 3, 2, 1 },
            { 7, 6, 5, 4, 3, 2 }
            };
            List<string> euValidDataranges = new List<string>()
            {
                "SF12BW125", // 0
                "SF11BW125", // 1
                "SF10BW125", // 2
                "SF9BW125", // 3
                "SF8BW125", // 4
                "SF7BW125", // 5
                "SF7BW250", // 6
                "50" // 7 FSK 50
            };

            EU868.MaxADRDataRate = 5;
            EU868.RegionLimits = new RegionLimits((min: 863, max: 870), euValidDataranges);

            US915 = new Region(
                LoRaRegion.US915,
                0x34,
                null, // no GFSK in US Band
                (frequency: 923.3, datr: 8),
                1,
                2,
                5,
                6,
                16384,
                64,
                32,
                (min: 1, max: 3));
            US915.DRtoConfiguration.Add(0, (configuration: "SF10BW125", maxPyldSize: 19));
            US915.DRtoConfiguration.Add(1, (configuration: "SF9BW125", maxPyldSize: 61));
            US915.DRtoConfiguration.Add(2, (configuration: "SF8BW125", maxPyldSize: 133));
            US915.DRtoConfiguration.Add(3, (configuration: "SF7BW125", maxPyldSize: 250));
            US915.DRtoConfiguration.Add(4, (configuration: "SF8BW500", maxPyldSize: 250));
            US915.DRtoConfiguration.Add(8, (configuration: "SF12BW500", maxPyldSize: 61));
            US915.DRtoConfiguration.Add(9, (configuration: "SF11BW500", maxPyldSize: 137));
            US915.DRtoConfiguration.Add(10, (configuration: "SF10BW500", maxPyldSize: 250));
            US915.DRtoConfiguration.Add(11, (configuration: "SF9BW500", maxPyldSize: 250));
            US915.DRtoConfiguration.Add(12, (configuration: "SF8BW500", maxPyldSize: 250));
            US915.DRtoConfiguration.Add(13, (configuration: "SF7BW500", maxPyldSize: 250));

            for (uint i = 0; i < 15; i++)
            {
                US915.TXPowertoMaxEIRP.Add(i, 30 - 2 * i);
            }

            US915.RX1DROffsetTable = new int[5, 4]
            {
            { 10, 9, 8, 8 },
            { 11, 10, 9, 8 },
            { 12, 11, 10, 9 },
            { 13, 12, 11, 10 },
            { 13, 13, 12, 11 },
            };
            List<string> usValidDataranges = new List<string>()
            {
                "SF10BW125", // 0
                "SF9BW125", // 1
                "SF8BW125", // 2
                "SF7BW125", // 3
                "SF8BW500", // 4
                "SF12BW500", // 8
                "SF11BW500", // 9
                "SF10BW500", // 10
                "SF9BW500", // 11
                "SF8BW500", // 12
                "SF8BW500" // 13
            };
            US915.MaxADRDataRate = 3;
            US915.RegionLimits = new RegionLimits((min: 902.3, max: 927.5), usValidDataranges);
        }

        public LoRaRegion LoRaRegion { get; set; }

        public byte LoRaSyncWord { get; private set; }

        public byte[] GFSKSyncWord { get; private set; }

        /// <summary>
        /// Gets or sets datarate to configuration and max payload size (M)
        /// max application payload size N should be N= M-8 bytes.
        /// This is in case of absence of Fopts field.
        /// </summary>
        public Dictionary<uint, (string configuration, uint maxPyldSize)> DRtoConfiguration { get; set; } = new Dictionary<uint, (string, uint)>();

        /// <summary>
        /// Gets or sets by default MaxEIRP is considered to be +16dBm.
        /// If the end-device cannot achieve 16dBm EIRP, the Max EIRP SHOULD be communicated to the network server using an out-of-band channel during the end-device commissioning process.
        /// </summary>
        public Dictionary<uint, uint> TXPowertoMaxEIRP { get; set; } = new Dictionary<uint, uint>();

        /// <summary>
        /// Gets or sets table to the get receive windows Offsets.
        /// X = RX1DROffset Upstream DR
        /// Y = Downstream DR in RX1 slot
        /// </summary>
        public int[,] RX1DROffsetTable { get; set; }

        /// <summary>
        /// Gets or sets default parameters for the RX2 receive Windows, This windows use a fix frequency and Data rate.
        /// </summary>
        public (double frequency, uint dr) RX2DefaultReceiveWindows { get; set; }

        /// <summary>
        /// Gets or sets default first receive windows. [sec]
        /// </summary>
        public uint Receive_delay1 { get; set; }

        /// <summary>
        /// Gets or sets default second receive Windows. Should be receive_delay1+1 [sec].
        /// </summary>
        public uint Receive_delay2 { get; set; }

        /// <summary>
        /// Gets or sets default Join Accept Delay for first Join Accept Windows.[sec]
        /// </summary>
        public uint Join_accept_delay1 { get; set; }

        /// <summary>
        /// Gets or sets default Join Accept Delay for second Join Accept Windows. [sec]
        /// </summary>
        public uint Join_accept_delay2 { get; set; }

        /// <summary>
        /// Gets or sets max fcnt gap between expected and received. [#frame]
        /// If this difference is greater than the value of MAX_FCNT_GAP then too many data frames have been lost then subsequent will be discarded
        /// </summary>
        public int Max_fcnt_gap { get; set; }

        /// <summary>
        /// Gets or sets number of uplink an end device can send without asking for an ADR acknowledgement request (set ADRACKReq bit to 1). [#frame]
        /// </summary>
        public uint Adr_ack_limit { get; set; }

        /// <summary>
        /// Gets or sets number of frames in which the network is required to respond to a ADRACKReq request. [#frame]
        /// If no response, during time select a lower data rate.
        /// </summary>
        public uint Adr_adr_delay { get; set; }

        /// <summary>
        /// Gets or sets timeout for ack transmissiont, tuple with (min,max). Value should be a delay between min and max. [sec, sec].
        /// If  an  end-­device  does  not  receive  a  frame  with  the  ACK  bit  set  in  one  of  the  two  receive  19   windows  immediately  following  the  uplink  transmission  it  may  resend  the  same  frame  with  20   the  same  payload  and  frame  counter  again  at  least  ACK_TIMEOUT  seconds  after  the  21   second  reception  window
        /// </summary>
        public (uint min, uint max) Ack_timeout { get; set; }

        /// <summary>
        /// Gets or sets the limits on the region to ensure valid properties
        /// </summary>
        public RegionLimits RegionLimits { get; set; }

        public int MaxADRDataRate { get; set; }

        public Region(LoRaRegion regionEnum, byte loRaSyncWord, byte[] gFSKSyncWord, (double frequency, uint datr) rx2DefaultReceiveWindows, uint receive_delay1, uint receive_delay2, uint join_accept_delay1, uint join_accept_delay2, int max_fcnt_gap, uint adr_ack_limit, uint adr_adr_delay, (uint min, uint max) ack_timeout)
        {
            this.LoRaRegion = regionEnum;
            this.Ack_timeout = ack_timeout;

            this.LoRaSyncWord = loRaSyncWord;
            this.GFSKSyncWord = gFSKSyncWord;

            this.RX2DefaultReceiveWindows = rx2DefaultReceiveWindows;
            this.Receive_delay1 = receive_delay1;
            this.Receive_delay2 = receive_delay2;
            this.Join_accept_delay1 = join_accept_delay1;
            this.Join_accept_delay2 = join_accept_delay2;
            this.Max_fcnt_gap = max_fcnt_gap;
            this.Adr_ack_limit = adr_ack_limit;
            this.Adr_adr_delay = adr_adr_delay;
        }

        /// <summary>
        /// Implement correct logic to get the correct transmission frequency based on the region.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted</param>
        public double GetDownstreamChannelFrequency(Rxpk upstreamChannel)
        {
            if (this.IsValidRxpk(upstreamChannel))
            {
                if (this.LoRaRegion == LoRaRegion.EU868)
                {
                    // in case of EU, you respond on same frequency as you sent data.
                    return upstreamChannel.Freq;
                }
                else if (this.LoRaRegion == LoRaRegion.US915)
                {
                    int upstreamChannelNumber;
                    // if DR4 the coding are different.
                    if (upstreamChannel.Datr == "SF8BW500")
                    {
                        // ==DR4
                        upstreamChannelNumber = 64 + (int)Math.Round((upstreamChannel.Freq - 903) / 1.6, 0);
                    }
                    else
                    {
                        // if not DR4 other encoding
                        upstreamChannelNumber = (int)((upstreamChannel.Freq - 902.3) / 0.2);
                    }

                    return Math.Round(923.3 + upstreamChannelNumber % 8 * 0.6, 1);
                }
            }

            return 0;
        }

        /// <summary>
        /// Method to calculate the RX2 DataRate and frequency.
        /// Those parameters can be set in the device twins, Server Twins, or it could be a regional feature.
        /// </summary>
        public (double freq, string datr) GetDownstreamRX2DRAndFreq(string devEUI, string nwkSrvRx2Dr, double nwkSrvRx2Freq, int? rx2DrFromTwins)
        {
            double freq = 0;
            string datr;

            // If the rx2 property is in twins, it is device specific and take precedence
            if (rx2DrFromTwins == null)
            {
                // Otherwise we check if we have some properties set on the server (server Specific)
                if (string.IsNullOrEmpty(nwkSrvRx2Dr))
                {
                    // If not we use the region default.
                    Logger.Log(devEUI, $"using standard second receive windows for join request", LogLevel.Information);
                    // using EU fix DR for RX2
                    freq = this.RX2DefaultReceiveWindows.frequency;
                    datr = this.DRtoConfiguration[RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.dr].configuration;
                }
                else
                {
                    Logger.Log(devEUI, $"using custom second receive windows for join request", LogLevel.Information);
                    freq = nwkSrvRx2Freq;
                    datr = nwkSrvRx2Dr;
                }
            }
            else
            {
                uint rx2Dr = (uint)rx2DrFromTwins;
                if (this.RegionLimits.IsCurrentDRIndexWithinAcceptableValue(rx2Dr))
                {
                    datr = this.DRtoConfiguration[rx2Dr].configuration;
                }
                else
                {
                    datr = this.DRtoConfiguration[RegionFactory.CurrentRegion.RX2DefaultReceiveWindows.dr].configuration;
                }

                // Todo add optional frequencies via Mac Commands
                freq = this.RX2DefaultReceiveWindows.frequency;
            }

            return (freq, datr);
        }

        /// <summary>
        /// Implement correct logic to get the downstream data rate based on the region.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted</param>
        public string GetDownstreamDR(Rxpk upstreamChannel, uint rx1DrOffset = 0)
        {
            if (this.IsValidRxpk(upstreamChannel))
            {
                if (this.LoRaRegion == LoRaRegion.EU868)
                {
                    // If the rx1 offset is a valid value we use it, otherwise we keep answering on normal datar
                    if (rx1DrOffset < this.RX1DROffsetTable.GetUpperBound(1))
                    {
                        // in case of EU, you respond on same frequency as you sent data.
                        return this.DRtoConfiguration[(uint)this.RX1DROffsetTable[this.GetDRFromFreqAndChan(upstreamChannel.Datr), rx1DrOffset]].configuration;
                    }
                    else
                    {
                        return upstreamChannel.Datr;
                    }
                }
                else if (this.LoRaRegion == LoRaRegion.US915)
                {
                    var dr = this.DRtoConfiguration.FirstOrDefault(x => x.Value.configuration == upstreamChannel.Datr).Key;
                    // TODO take care of rx1droffset
                    if (dr >= 0 && dr < 5)
                    {
                        var (configuration, maxPyldSize) = dr != 4 ? this.DRtoConfiguration[10 + dr] : this.DRtoConfiguration[13];
                        return configuration;
                    }
                    else
                    {
                        throw new RegionLimitException($"Datarate {upstreamChannel.Datr} in {this.LoRaRegion} region was not within the acceptable range of upstream datarates.", RegionLimitExceptionType.Datarate);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Implement correct logic to get the maximum payload size based on the datr/configuration.
        /// </summary>
        /// <param name="datr">the datr/configuration with which the message was transmitted</param>
        public uint GetMaxPayloadSize(string datr)
        {
            var maxPayloadSize = this.DRtoConfiguration.FirstOrDefault(x => x.Value.configuration == datr).Value.maxPyldSize;

            return maxPayloadSize;
        }

        /// <summary>
        /// This method Check that a received packet is within the correct frenquency range and has a valid Datr.
        /// </summary>
        private bool IsValidRxpk(Rxpk rxpk)
        {
            if (this.LoRaRegion == LoRaRegion.EU868)
            {
                if (rxpk.Freq < EU868.RegionLimits.FrequencyRange.min ||
                    rxpk.Freq > EU868.RegionLimits.FrequencyRange.max ||
                    !EU868.RegionLimits.DatarateRange.Contains(rxpk.Datr))
                {
                    Logger.Log("A Rxpk packet not fitting the current region configuration was received, aborting processing.", LogLevel.Error);
                    return false;
                }
            }
            else if (this.LoRaRegion == LoRaRegion.US915)
            {
                if (rxpk.Freq < US915.RegionLimits.FrequencyRange.min ||
                    rxpk.Freq > US915.RegionLimits.FrequencyRange.max ||
                    !US915.RegionLimits.DatarateRange.Contains(rxpk.Datr))
                {
                    Logger.Log("A Rxpk packet not fitting the current region configuration was received, aborting processing.", LogLevel.Error);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get Datarate number from SF#BW# string
        /// </summary>
        public int GetDRFromFreqAndChan(string datr)
        {
            return (int)this.DRtoConfiguration.FirstOrDefault(x => x.Value.configuration == datr).Key;
        }
    }
}
