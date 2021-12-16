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

    public abstract record Region
    {
        private const ushort MAX_RX_DELAY = 15;

        protected const double EPSILON = 0.00001;

        public LoRaRegionType LoRaRegion { get; init; }

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
        public Dictionary<uint, double> TXPowertoMaxEIRP { get; } = new Dictionary<uint, double>();

        /// <summary>
        /// Gets or sets table to the get receive windows Offsets.
        /// X = RX1DROffset Upstream DR
        /// Y = Downstream DR in RX1 slot.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<int>> RX1DROffsetTable { get; init; }

        /// <summary>
        /// Gets or sets default first receive windows. [sec].
        /// </summary>
        public uint ReceiveDelay1 { get; init; }

        /// <summary>
        /// Gets or sets default second receive Windows. Should be receive_delay1+1 [sec].
        /// </summary>
        public uint ReceiveDelay2 { get; init; }

        /// <summary>
        /// Gets or sets default Join Accept Delay for first Join Accept Windows.[sec].
        /// </summary>
        public uint JoinAcceptDelay1 { get; init; }

        /// <summary>
        /// Gets or sets default Join Accept Delay for second Join Accept Windows. [sec].
        /// </summary>
        public uint JoinAcceptDelay2 { get; init; }

        /// <summary>
        /// Gets or sets max fcnt gap between expected and received. [#frame]
        /// If this difference is greater than the value of MAX_FCNT_GAP then too many data frames have been lost then subsequent will be discarded.
        /// </summary>
        public int MaxFcntGap { get; init; }

        /// <summary>
        /// Gets or sets number of uplink an end device can send without asking for an ADR acknowledgement request (set ADRACKReq bit to 1). [#frame].
        /// </summary>
        public uint AdrAckLimit { get; init; }

        /// <summary>
        /// Gets or sets number of frames in which the network is required to respond to a ADRACKReq request. [#frame]
        /// If no response, during time select a lower data rate.
        /// </summary>
        public uint AdrAdrDelay { get; init; }

        /// <summary>
        /// Gets or sets timeout for ack transmissiont, tuple with (min,max). Value should be a delay between min and max. [sec, sec].
        /// If  an  end-­device  does  not  receive  a  frame  with  the  ACK  bit  set  in  one  of  the  two  receive  19   windows  immediately  following  the  uplink  transmission  it  may  resend  the  same  frame  with  20   the  same  payload  and  frame  counter  again  at  least  ACK_TIMEOUT  seconds  after  the  21   second  reception  window.
        /// </summary>
        public (uint min, uint max) RetransmitTimeout { get; init; }

        /// <summary>
        /// Gets or sets the limits on the region to ensure valid properties.
        /// </summary>
        public RegionLimits RegionLimits { get; init; }

        /// <summary>
        /// Gets or sets set the Max ADR datarate acceptable, this is not necessarelly the highest in the region hence we need an additional param.
        /// </summary>
        public int MaxADRDataRate { get; init; }

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
        /// This default implementation is used by all regions which do not have the concept of join channels.
        /// </summary>
        /// <param name="joinChannel">Channel on which the join request was received.</param>
        public virtual bool TryGetJoinChannelIndex(Hertz frequency, out int channelIndex)
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
        /// <param name="upstreamDataRate">Ustream data rate.</param>
        /// <param name="deviceJoinInfo">Join info for the device, if applicable.</param>
        public abstract bool TryGetDownstreamChannelFrequency(Hertz upstreamFrequency, out Hertz downstreamFrequency, ushort? upstreamDataRate = null, DeviceJoinInfo deviceJoinInfo = null);

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
                if (IsValidRX1DROffset(rx1DrOffset))
                {
                    return DRtoConfiguration[(ushort)RX1DROffsetTable[GetDRFromFreqAndChan(upstreamChannel.Datr)][rx1DrOffset]].configuration;
                }
                else
                {
                    throw new LoRaProcessingException($"RX1 data rate offset was set to an invalid value {rx1DrOffset}; " +
                        $"maximum allowed offset is {RX1DROffsetTable[0].Count - 1}", LoRaProcessingErrorCode.InvalidDataRateOffset);
                }
            }

            throw new LoRaProcessingException($"Invalid upstream channel: {upstreamChannel.Freq}, {upstreamChannel.Datr}.");
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
                if (IsValidRX1DROffset(rx1DrOffset))
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
        public Hertz GetDownstreamRX2Freq(Hertz? nwkSrvRx2Freq, ILogger logger, DeviceJoinInfo deviceJoinInfo = null)
        {
            // resolve frequency to gateway if set to region's default
            if (nwkSrvRx2Freq is { } someNwkSrvRx2Freq)
            {
                logger.LogDebug($"using custom gateway RX2 frequency {someNwkSrvRx2Freq}", LogLevel.Debug);
                return someNwkSrvRx2Freq;
            }
            else
            {
                // default frequency
                (var defaultFrequency, _) = GetDefaultRX2ReceiveWindow(deviceJoinInfo);
                logger.LogDebug($"using standard region RX2 frequency {defaultFrequency}");
                return defaultFrequency;
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
        public string GetDownstreamRX2DataRate(string devEUI, string nwkSrvRx2Dr, ushort? rx2DrFromTwins, ILogger logger, DeviceJoinInfo deviceJoinInfo = null)
        {
            // If the rx2 datarate property is in twins, we take it from there
            if (rx2DrFromTwins.HasValue)
            {
                if (RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(rx2DrFromTwins))
                {
                    var datr = DRtoConfiguration[rx2DrFromTwins.Value].configuration;
                    logger.LogDebug($"using device twins rx2: {rx2DrFromTwins.Value}, datr: {datr}");
                    return datr;
                }
                else
                {
                    logger.LogError($"device twins rx2 ({rx2DrFromTwins.Value}) is invalid");
                }
            }
            else
            {
                // Otherwise we check if we have some properties set on the server (server Specific)
                if (!string.IsNullOrEmpty(nwkSrvRx2Dr))
                {
                    var datr = nwkSrvRx2Dr;
                    logger.LogDebug($"using custom gateway RX2 datarate {datr}");
                    return datr;
                }
            }

            // if no settings was set we use region default.
            var rx2ReceiveWindow = GetDefaultRX2ReceiveWindow(deviceJoinInfo);
            var defaultDatr = DRtoConfiguration[rx2ReceiveWindow.DataRate].configuration;
            logger.LogDebug($"using standard region RX2 datarate {defaultDatr}");
            return defaultDatr;
        }

        /// <summary>
        /// Get downstream RX2 data rate.
        /// </summary>
        /// <param name="nwkSrvRx2Dr">The network server rx2 datarate.</param>
        /// <param name="rx2DrFromTwins">RX2 datarate value from twins.</param>
        /// <returns>The RX2 data rate.</returns>
        public ushort GetDownstreamRX2DataRate(ushort? nwkSrvRx2Dr, ushort? rx2DrFromTwins, ILogger logger, DeviceJoinInfo deviceJoinInfo = null)
        {
            // If the rx2 datarate property is in twins, we take it from there
            if (rx2DrFromTwins.HasValue)
            {
                if (RegionLimits.IsCurrentDownstreamDRIndexWithinAcceptableValue(rx2DrFromTwins))
                {
                    var datr = rx2DrFromTwins.Value;
                    logger.LogDebug($"using device twins rx2: {rx2DrFromTwins.Value}, datr: {datr}");
                    return datr;
                }
                else
                {
                    logger.LogError($"device twins rx2 ({rx2DrFromTwins.Value}) is invalid");
                }
            }
            else
            {
                // Otherwise we check if we have some properties set on the server (server-specific)
                if (nwkSrvRx2Dr.HasValue)
                {
                    var datr = nwkSrvRx2Dr.Value;
                    logger.LogDebug($"using custom gateway RX2 datarate {datr}");
                    return datr;
                }
            }

            // If no settings was set we use region default.
            var defaultDatr = GetDefaultRX2ReceiveWindow(deviceJoinInfo).DataRate;
            logger.LogDebug($"using standard region RX2 datarate {defaultDatr}");
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

            if (rxpk.FreqHertz < RegionLimits.FrequencyRange.Min ||
                rxpk.FreqHertz > RegionLimits.FrequencyRange.Max ||
                !RegionLimits.IsCurrentUpstreamDRValueWithinAcceptableValue(rxpk.Datr))
            {
                return false;
            }

            return true;
        }

        protected bool IsValidUpstreamFrequency(Hertz frequency) => RegionLimits.FrequencyRange.Min <= frequency && frequency <= RegionLimits.FrequencyRange.Max;

        protected bool IsValidUpstreamDataRate(ushort dataRate) => RegionLimits.IsCurrentUpstreamDRIndexWithinAcceptableValue(dataRate);

        /// <summary>
        /// Get Datarate number from SF#BW# string.
        /// </summary>
        [Obsolete("#655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done.")]
        public int GetDRFromFreqAndChan(string datr)
        {
            return DRtoConfiguration.FirstOrDefault(x => x.Value.configuration == datr).Key;
        }

        public bool IsValidRX1DROffset(int rx1DrOffset) => rx1DrOffset >= 0 && rx1DrOffset <= RX1DROffsetTable[0].Count - 1;

        public static bool IsValidRXDelay(ushort desiredRXDelay) => desiredRXDelay is >= 0 and <= MAX_RX_DELAY;
    }
}
