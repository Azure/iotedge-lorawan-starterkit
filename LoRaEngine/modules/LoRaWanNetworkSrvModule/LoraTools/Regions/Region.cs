// Copyright (c) Microsoft. All rights reserved.
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
        private const ushort MAX_RX_DELAY = 15;

        public LoRaRegionType LoRaRegion { get; set; }

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

        /// <summary>
        /// Gets or sets set the Max ADR datarate acceptable, this is not necessarelly the highest in the region hence we need an additional param
        /// </summary>
        public int MaxADRDataRate { get; set; }

        public Region(LoRaRegionType regionEnum, byte loRaSyncWord, byte[] gFSKSyncWord, (double frequency, uint datr) rx2DefaultReceiveWindows, uint receive_delay1, uint receive_delay2, uint join_accept_delay1, uint join_accept_delay2, int max_fcnt_gap, uint adr_ack_limit, uint adr_adr_delay, (uint min, uint max) ack_timeout)
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
        public bool TryGetUpstreamChannelFrequency(Rxpk upstreamChannel, out double frequency)
        {
            frequency = 0;

            if (this.IsValidUpstreamRxpk(upstreamChannel))
            {
                if (this.LoRaRegion == LoRaRegionType.EU868)
                {
                    // in case of EU, you respond on same frequency as you sent data.
                    frequency = upstreamChannel.Freq;
                    return true;
                }
                else if (this.LoRaRegion == LoRaRegionType.US915)
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

                    frequency = Math.Round(923.3 + upstreamChannelNumber % 8 * 0.6, 1);
                    return true;
                }
            }

            return false;
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
                    Logger.Log(devEUI, $"using standard second receive windows for join request", LogLevel.Debug);
                    // using EU fix DR for RX2
                    freq = this.RX2DefaultReceiveWindows.frequency;
                    datr = this.DRtoConfiguration[this.RX2DefaultReceiveWindows.dr].configuration;
                }
                else
                {
                    Logger.Log(devEUI, $"using custom second receive windows for join request", LogLevel.Debug);
                    freq = nwkSrvRx2Freq;
                    datr = nwkSrvRx2Dr;
                }
            }
            else
            {
                uint rx2Dr = (uint)rx2DrFromTwins;
                if (this.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(rx2Dr))
                {
                    datr = this.DRtoConfiguration[rx2Dr].configuration;
                }
                else
                {
                    datr = this.DRtoConfiguration[this.RX2DefaultReceiveWindows.dr].configuration;
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
            if (this.IsValidUpstreamRxpk(upstreamChannel))
            {
                // If the rx1 offset is a valid value we use it, otherwise we keep answering on normal datar
                if (rx1DrOffset <= this.RX1DROffsetTable.GetUpperBound(1))
                {
                    // in case of EU, you respond on same frequency as you sent data.
                    return this.DRtoConfiguration[(uint)this.RX1DROffsetTable[this.GetDRFromFreqAndChan(upstreamChannel.Datr), rx1DrOffset]].configuration;
                }
                else
                {
                    return upstreamChannel.Datr;
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
        private bool IsValidUpstreamRxpk(Rxpk rxpk)
        {
            if (rxpk.Freq < this.RegionLimits.FrequencyRange.min ||
                rxpk.Freq > this.RegionLimits.FrequencyRange.max ||
                !this.RegionLimits.IsCurrentUpstreamDRValueWithinAcceptableValue(rxpk.Datr))
            {
                Logger.Log("A Rxpk packet not fitting the current region configuration was received, aborting processing.", LogLevel.Error);
                return false;
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

        public bool IsValidRX1DROffset(uint rx1DrOffset) => rx1DrOffset >= 0 && rx1DrOffset <= this.RX1DROffsetTable.GetUpperBound(1);

        public bool IsValidRXDelay(ushort desiredRXDelay)
        {
            return desiredRXDelay >= 0 && desiredRXDelay <= MAX_RX_DELAY;
        }
    }
}
