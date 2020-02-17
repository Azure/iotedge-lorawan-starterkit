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
        public Dictionary<ushort, (string configuration, uint maxPyldSize)> DRtoConfiguration { get; set; } = new Dictionary<ushort, (string, uint)>();

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
        public (double frequency, ushort dr) RX2DefaultReceiveWindows { get; set; }

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

        public Region(LoRaRegionType regionEnum, byte loRaSyncWord, byte[] gFSKSyncWord, (double frequency, ushort datr) rx2DefaultReceiveWindows, uint receive_delay1, uint receive_delay2, uint join_accept_delay1, uint join_accept_delay2, int max_fcnt_gap, uint adr_ack_limit, uint adr_adr_delay, (uint min, uint max) ack_timeout)
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
        /// Get the downstream RX2 frequency.
        /// </summary>
        /// <param name="devEUI">the device id.</param>
        /// <param name="nwkSrvRx2Freq">the value of the rx2freq env var on the nwk srv.</param>
        /// <returns>rx2 freq.</returns>
        public double GetDownstreamRX2Freq(string devEUI, double? nwkSrvRx2Freq)
        {
            // resolve frequency to gateway if setted to region's default
            if (nwkSrvRx2Freq.HasValue)
            {
                Logger.Log(devEUI, $"using custom gateway RX2 frequency {nwkSrvRx2Freq}", LogLevel.Debug);
                return nwkSrvRx2Freq.Value;
            }
            else
            {
                // default frequency
                Logger.Log(devEUI, $"using standard region RX2 frequency {this.RX2DefaultReceiveWindows.frequency}", LogLevel.Debug);
                return this.RX2DefaultReceiveWindows.frequency;
            }
        }

        /// <summary>
        /// Get downstream RX2 datarate
        /// </summary>
        /// <param name="devEUI">the device id.</param>
        /// <param name="nwkSrvRx2Dr">the network server rx2 datarate.</param>
        /// <param name="rx2DrFromTwins">rx2 datarate value from twins.</param>
        /// <returns>the rx2 datarate.</returns>
        public string GetDownstreamRX2Datarate(string devEUI, string nwkSrvRx2Dr, ushort? rx2DrFromTwins)
        {
            // If the rx2 datarate property is in twins, we take it from there
            if (rx2DrFromTwins.HasValue)
            {
                if (this.RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(rx2DrFromTwins))
                {
                    var datr = this.DRtoConfiguration[rx2DrFromTwins.Value].configuration;
                    Logger.Log(devEUI, $"using device twins rx2: {rx2DrFromTwins.Value}, datr: {datr}", LogLevel.Debug);
                    return datr;
                }
                else
                {
                    var datr = this.DRtoConfiguration[this.RX2DefaultReceiveWindows.dr].configuration;
                    Logger.Log(devEUI, $"device twins rx2 ({rx2DrFromTwins.Value}) is invalid, using default: {this.RX2DefaultReceiveWindows.dr}, datr: {datr}", LogLevel.Debug);
                    return datr;
                }
            }
            else
            {
                // Otherwise we check if we have some properties set on the server (server Specific)
                if (string.IsNullOrEmpty(nwkSrvRx2Dr))
                {
                    // If not we use the region default.
                    var datr = this.DRtoConfiguration[this.RX2DefaultReceiveWindows.dr].configuration;
                    Logger.Log(devEUI, $"using standard region RX2 datarate {datr}", LogLevel.Debug);
                    return datr;
                }
                else
                {
                    var datr = nwkSrvRx2Dr;
                    Logger.Log(devEUI, $"using custom gateway RX2 datarate {datr}", LogLevel.Debug);
                    return datr;
                }
            }
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
                    return this.DRtoConfiguration[(ushort)this.RX1DROffsetTable[this.GetDRFromFreqAndChan(upstreamChannel.Datr), rx1DrOffset]].configuration;
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
