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

    public abstract class Region
    {
        private const ushort MAX_RX_DELAY = 15;

        public LoRaRegionType LoRaRegion { get; set; }

        /// <summary>
        /// Gets or sets datarate to configuration and max payload size (M)
        /// max application payload size N should be N= M-8 bytes.
        /// This is in case of absence of Fopts field.
        /// </summary>
        public Dictionary<ushort, (string configuration, uint maxPyldSize)> DRtoConfiguration { get; } = new Dictionary<ushort, (string, uint)>();

        /// <summary>
        /// Gets or sets by default MaxEIRP is considered to be +16dBm.
        /// If the end-device cannot achieve 16dBm EIRP, the Max EIRP SHOULD be communicated to the network server using an out-of-band channel during the end-device commissioning process.
        /// </summary>
        public Dictionary<uint, uint> TXPowertoMaxEIRP { get; } = new Dictionary<uint, uint>();

        /// <summary>
        /// Gets or sets table to the get receive windows Offsets.
        /// X = RX1DROffset Upstream DR
        /// Y = Downstream DR in RX1 slot.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<int>> RX1DROffsetTable { get; set; }

        /// <summary>
        /// Gets or sets default first receive windows. [sec].
        /// </summary>
        public uint ReceiveDelay1 { get; set; }

        /// <summary>
        /// Gets or sets default second receive Windows. Should be receive_delay1+1 [sec].
        /// </summary>
        public uint ReceiveDelay2 { get; set; }

        /// <summary>
        /// Gets or sets default Join Accept Delay for first Join Accept Windows.[sec].
        /// </summary>
        public uint JoinAcceptDelay1 { get; set; }

        /// <summary>
        /// Gets or sets default Join Accept Delay for second Join Accept Windows. [sec].
        /// </summary>
        public uint JoinAcceptDelay2 { get; set; }

        /// <summary>
        /// Gets or sets max fcnt gap between expected and received. [#frame]
        /// If this difference is greater than the value of MAX_FCNT_GAP then too many data frames have been lost then subsequent will be discarded.
        /// </summary>
        public int MaxFcntGap { get; set; }

        /// <summary>
        /// Gets or sets number of uplink an end device can send without asking for an ADR acknowledgement request (set ADRACKReq bit to 1). [#frame].
        /// </summary>
        public uint AdrAckLimit { get; set; }

        /// <summary>
        /// Gets or sets number of frames in which the network is required to respond to a ADRACKReq request. [#frame]
        /// If no response, during time select a lower data rate.
        /// </summary>
        public uint AdrAdrDelay { get; set; }

        /// <summary>
        /// Gets or sets timeout for ack transmissiont, tuple with (min,max). Value should be a delay between min and max. [sec, sec].
        /// If  an  end-­device  does  not  receive  a  frame  with  the  ACK  bit  set  in  one  of  the  two  receive  19   windows  immediately  following  the  uplink  transmission  it  may  resend  the  same  frame  with  20   the  same  payload  and  frame  counter  again  at  least  ACK_TIMEOUT  seconds  after  the  21   second  reception  window.
        /// </summary>
        public (uint min, uint max) RetransmitTimeout { get; set; }

        /// <summary>
        /// Gets or sets the limits on the region to ensure valid properties.
        /// </summary>
        public RegionLimits RegionLimits { get; set; }

        /// <summary>
        /// Gets or sets set the Max ADR datarate acceptable, this is not necessarelly the highest in the region hence we need an additional param.
        /// </summary>
        public int MaxADRDataRate { get; set; }

        protected Region(LoRaRegionType regionEnum)
        {
            LoRaRegion = regionEnum;
            RetransmitTimeout = (min: 1, max: 3);

            ReceiveDelay1 = 1;
            ReceiveDelay2 = 2;
            JoinAcceptDelay1 = 5;
            JoinAcceptDelay2 = 6;
            MaxFcntGap = 16384;
            AdrAckLimit = 64;
            AdrAdrDelay = 32;
        }

        /// <summary>
        /// Returns join channel index matching the frequency of the join request.
        /// </summary>
        /// <param name="joinChannel">Channel on which the join request was received.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public virtual bool TryGetJoinChannelIndex(Rxpk joinChannel, out int channelIndex)
        {
            channelIndex = -1;
            return false;
        }

        /// <summary>
        /// Returns join channel index matching the frequency of the join request.
        /// </summary>
        /// <param name="joinChannel">Channel on which the join request was received.</param>
        public virtual bool TryGetJoinChannelIndex(double frequency, out int channelIndex)
        {
            channelIndex = -1;
            return false;
        }

        /// <summary>
        /// Implements logic to get the correct downstream transmission frequency for the given region based on the upstream channel frequency.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the upstream message was transmitted.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public abstract bool TryGetDownstreamChannelFrequency(Rxpk upstreamChannel, out double frequency, DeviceJoinInfo deviceJoinInfo = null);

        /// <summary>
        /// Implements logic to get the correct downstream transmission frequency for the given region based on the upstream channel frequency.
        /// </summary>
        /// <param name="upstreamFrequency">Frequency of the upstream message.</param>
        /// <param name="dataRate">Ustream data rate.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public abstract bool TryGetDownstreamChannelFrequency(double upstreamFrequency, ushort dataRate, out double downstreamFrequency, DeviceJoinInfo deviceJoinInfo = null);

        /// <summary>
        /// Returns downstream data rate based on the upstream channel and RX1 DR offset.
        /// </summary>
        /// <param name="upstreamChannel">the channel at which the message was transmitted.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public string GetDownstreamDataRate(Rxpk upstreamChannel, int rx1DrOffset = 0)
        {
            if (upstreamChannel is null) throw new ArgumentNullException(nameof(upstreamChannel));

            if (IsValidUpstreamRxpk(upstreamChannel))
            {
                // If the rx1 offset is a valid value we use it, otherwise we throw an exception
                if (rx1DrOffset <= RX1DROffsetTable[0].Count - 1)
                {
                    return DRtoConfiguration[(ushort)RX1DROffsetTable[GetDRFromFreqAndChan(upstreamChannel.Datr)][rx1DrOffset]].configuration;
                }
                else
                {
                    throw new LoRaProcessingException($"RX1 data rate offset was set to an invalid value {rx1DrOffset}; " +
                        $"maximum allowed offset is {RX1DROffsetTable[0].Count - 1}", LoRaProcessingErrorCode.InvalidDataRateOffset);
                }
            }

            throw new LoRaProcessingException($"Invalid upstream data rate {upstreamChannel.Datr}", LoRaProcessingErrorCode.InvalidDataRate);
        }

        /// <summary>
        /// Returns downstream data rate based on the upstream data rate and RX1 DR offset.
        /// </summary>
        /// <param name="dataRate">Data rate at which the message was transmitted.</param>
        /// <param name="rx1DrOffset">RX1 offset to be used for calculating downstream data rate.</param>
        public ushort GetDownstreamDataRate(ushort dataRate, int rx1DrOffset = 0)
        {
            if (IsValidUpstreamDataRate(dataRate))
            {
                // If the rx1 offset is a valid value we use it, otherwise we throw an exception
                if (rx1DrOffset <= RX1DROffsetTable[0].Count - 1)
                {
                    return (ushort)RX1DROffsetTable[dataRate][rx1DrOffset];
                }
                else
                {
                    throw new LoRaProcessingException($"RX1 data rate offset was set to an invalid value {rx1DrOffset}; " +
                           $"maximum allowed offset is {RX1DROffsetTable[0].Count - 1}", LoRaProcessingErrorCode.InvalidDataRateOffset);
                }
            }

            throw new LoRaProcessingException($"Invalid upstream data rate {dataRate}", LoRaProcessingErrorCode.InvalidDataRate);
        }

        /// <summary>
        /// Returns the default RX2 receive window parameters - frequency and data rate.
        /// </summary>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public abstract RX2ReceiveWindow GetDefaultRX2ReceiveWindow(DeviceJoinInfo deviceJoinInfo = null);

        /// <summary>
        /// Get the downstream RX2 frequency.
        /// </summary>
        /// <param name="devEUI">the device id.</param>
        /// <param name="nwkSrvRx2Freq">the value of the rx2freq env var on the nwk srv.</param>
        /// <param name="deviceJoinInfo">join info for the device, if applicable.</param>
        /// <returns>rx2 freq.</returns>
        public double GetDownstreamRX2Freq(string devEUI, double? nwkSrvRx2Freq, DeviceJoinInfo deviceJoinInfo = null)
        {
            // resolve frequency to gateway if set to region's default
            if (nwkSrvRx2Freq.HasValue)
            {
                StaticLogger.Log(devEUI, $"using custom gateway RX2 frequency {nwkSrvRx2Freq}", LogLevel.Debug);
                return nwkSrvRx2Freq.Value;
            }
            else
            {
                // default frequency
                var rx2ReceiveWindow = GetDefaultRX2ReceiveWindow(deviceJoinInfo);
                StaticLogger.Log(devEUI, $"using standard region RX2 frequency {rx2ReceiveWindow.Frequency}", LogLevel.Debug);
                return rx2ReceiveWindow.Frequency;
            }
        }

        /// <summary>
        /// Get downstream RX2 datarate.
        /// </summary>
        /// <param name="devEUI">the device id.</param>
        /// <param name="nwkSrvRx2Dr">the network server rx2 datarate.</param>
        /// <param name="rx2DrFromTwins">rx2 datarate value from twins.</param>
        /// <param name="deviceJoinInfo">join info for the device, if applicable.</param>
        /// <returns>the rx2 datarate.</returns>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done")]
        public string GetDownstreamRX2DataRate(string devEUI, string nwkSrvRx2Dr, ushort? rx2DrFromTwins, DeviceJoinInfo deviceJoinInfo = null)
        {
            // If the rx2 datarate property is in twins, we take it from there
            if (rx2DrFromTwins.HasValue)
            {
                if (RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(rx2DrFromTwins))
                {
                    var datr = DRtoConfiguration[rx2DrFromTwins.Value].configuration;
                    StaticLogger.Log(devEUI, $"using device twins rx2: {rx2DrFromTwins.Value}, datr: {datr}", LogLevel.Debug);
                    return datr;
                }
                else
                {
                    StaticLogger.Log(devEUI, $"device twins rx2 ({rx2DrFromTwins.Value}) is invalid", LogLevel.Error);
                }
            }
            else
            {
                // Otherwise we check if we have some properties set on the server (server Specific)
                if (!string.IsNullOrEmpty(nwkSrvRx2Dr))
                {
                    var datr = nwkSrvRx2Dr;
                    StaticLogger.Log(devEUI, $"using custom gateway RX2 datarate {datr}", LogLevel.Debug);
                    return datr;
                }
            }

            // if no settings was set we use region default.
            var rx2ReceiveWindow = GetDefaultRX2ReceiveWindow(deviceJoinInfo);
            var defaultDatr = DRtoConfiguration[rx2ReceiveWindow.DataRate].configuration;
            StaticLogger.Log(devEUI, $"using standard region RX2 datarate {defaultDatr}", LogLevel.Debug);
            return defaultDatr;
        }

        /// <summary>
        /// Get downstream RX2 datarate.
        /// </summary>
        /// <param name="devEUI">the device id.</param>
        /// <param name="nwkSrvRx2Dr">the network server rx2 datarate.</param>
        /// <param name="rx2DrFromTwins">rx2 datarate value from twins.</param>
        /// <returns>the rx2 datarate.</returns>
        public ushort GetDownstreamRX2DataRate(string devEUI, ushort? nwkSrvRx2Dr, ushort? rx2DrFromTwins, DeviceJoinInfo deviceJoinInfo = null)
        {
            // If the rx2 datarate property is in twins, we take it from there
            if (rx2DrFromTwins.HasValue)
            {
                if (RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(rx2DrFromTwins))
                {
                    var datr = rx2DrFromTwins.Value;
                    StaticLogger.Log(devEUI, $"using device twins rx2: {rx2DrFromTwins.Value}, datr: {datr}", LogLevel.Debug);
                    return datr;
                }
                else
                {
                    StaticLogger.Log(devEUI, $"device twins rx2 ({rx2DrFromTwins.Value}) is invalid", LogLevel.Error);
                }
            }
            else
            {
                // Otherwise we check if we have some properties set on the server (server Specific)
                if (nwkSrvRx2Dr.HasValue)
                {
                    var datr = nwkSrvRx2Dr.Value;
                    StaticLogger.Log(devEUI, $"using custom gateway RX2 datarate {datr}", LogLevel.Debug);
                    return datr;
                }
            }

            // if no settings was set we use region default.
            var defaultDatr = GetDefaultRX2ReceiveWindow(deviceJoinInfo).DataRate;
            StaticLogger.Log(devEUI, $"using standard region RX2 datarate {defaultDatr}", LogLevel.Debug);
            return defaultDatr;
        }

        /// <summary>
        /// Implement correct logic to get the maximum payload size based on the datr/configuration.
        /// </summary>
        /// <param name="datr">the datr/configuration with which the message was transmitted.</param>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public uint GetMaxPayloadSize(string datr)
        {
            var maxPayloadSize = DRtoConfiguration.FirstOrDefault(x => x.Value.configuration == datr).Value.maxPyldSize;
            return maxPayloadSize;
        }

        /// <summary>
        /// Implement correct logic to get the maximum payload size based on the datr/configuration.
        /// </summary>
        /// <param name="datr">the datr/configuration with which the message was transmitted.</param>
        public uint GetMaxPayloadSize(ushort datr) => DRtoConfiguration[datr].maxPyldSize;

        /// <summary>
        /// This method Check that a received packet is within the correct frenquency range and has a valid Datr.
        /// </summary>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        protected bool IsValidUpstreamRxpk(Rxpk rxpk)
        {
            if (rxpk is null) throw new ArgumentNullException(nameof(rxpk));

            if (rxpk.Freq < RegionLimits.FrequencyRange.min ||
                rxpk.Freq > RegionLimits.FrequencyRange.max ||
                !RegionLimits.IsCurrentUpstreamDRValueWithinAcceptableValue(rxpk.Datr))
            {
                StaticLogger.Log("A Rxpk packet not fitting the current region configuration was received, aborting processing.", LogLevel.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// This method checks that a received message is within the correct frenquency range and has a valid datarate.
        /// </summary>
        /// <param name="frequency">Frequency on which the message was transmitted.</param>
        /// <param name="dataRate">Data rate with which the message was transmitted.</param>
        protected bool IsValidUpstreamFrequencyAndDataRate(double frequency, ushort dataRate)
        {
            if (!IsValidUpstreamFrequency(frequency) || !IsValidUpstreamDataRate(dataRate))
            {
                StaticLogger.Log("A upstream message not fitting the current region configuration was received, aborting processing.", LogLevel.Error);
                return false;
            }

            return true;
        }

        private bool IsValidUpstreamFrequency(double frequency) => RegionLimits.FrequencyRange.min <= frequency && frequency <= RegionLimits.FrequencyRange.max;

        private bool IsValidUpstreamDataRate(ushort dataRate) => RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue(dataRate);

        /// <summary>
        /// Get Datarate number from SF#BW# string.
        /// </summary>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public int GetDRFromFreqAndChan(string datr)
        {
            return DRtoConfiguration.FirstOrDefault(x => x.Value.configuration == datr).Key;
        }

        public bool IsValidRX1DROffset(uint rx1DrOffset) => rx1DrOffset >= 0 && rx1DrOffset <= RX1DROffsetTable[0].Count - 1;

        public static bool IsValidRXDelay(ushort desiredRXDelay) => desiredRXDelay is >= 0 and <= MAX_RX_DELAY;
    }
}
