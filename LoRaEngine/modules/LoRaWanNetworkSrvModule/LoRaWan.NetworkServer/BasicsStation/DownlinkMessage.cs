// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.LoRaPhysical
{
    using System;
    using LoRaWan;

    /// <summary>
    /// Model class for a Downlink message for the Basic Station.
    /// </summary>
    public class DownlinkMessage
    {
        public DevEui DevEui { get; }

        /// This rx delay control the time we need to wait before sending.
        /// It is typically 0 for class C devices.
        /// It is a uint value 0 - 15 used by basic station to know when to first try to send a message.
        public uint LnsRxDelay { get; }

        public ulong Xtime { get; }

        public uint? AntennaPreference { get; }

        public DataRateIndex DataRateRx1 { get; }

        public DataRateIndex DataRateRx2 { get; }

        public Hertz FrequencyRx1 { get; }

        public Hertz FrequencyRx2 { get; }

#pragma warning disable CA1819 // Properties should not return arrays
        public byte[] Data { get; }
#pragma warning restore CA1819 // Properties should not return arrays

        public StationEui StationEui { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DownlinkMessage"/> class.
        /// This method is used in case of a response to a upstream message.
        /// </summary>
        /// <returns><see cref="DownlinkMessage"/> object ready to be sent.</returns>
        public DownlinkMessage(byte[] payload, ulong xtime, DataRateIndex datrRx1, DataRateIndex datrRx2, Hertz freqRx1, Hertz freqRx2, DevEui devEui, ushort lnsRxDelay = 0, StationEui stationEui = default, uint? antennaPreference = null)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            Data = payload;

            DevEui = devEui;
            LnsRxDelay = lnsRxDelay;
            AntennaPreference = antennaPreference;
            StationEui = stationEui;
            Xtime = xtime;
            DataRateRx1 = datrRx1;
            DataRateRx2 = datrRx2;
            FrequencyRx1 = freqRx1;
            FrequencyRx2 = freqRx2;
        }
    }
}
