// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.LoRaPhysical
{
    using System;
    using LoRaWan;
    using LoRaWan.NetworkServer;
    using Utils;

    /// <summary>
    /// Model class for a Downlink message for the Basic Station.
    /// </summary>
    public class DownlinkMessage
    {
        public DevEui DevEui { get; }

        /// This rx delay control the time we need to wait before sending.
        /// It is typically 0 for class C devices.
        /// It is a uint value 0 - 15 used by basic station to know when to first try to send a message.
        public RxDelay LnsRxDelay { get; }

        public LoRaDeviceClassType DeviceClassType { get; }

        public ulong Xtime { get; }

        public uint? AntennaPreference { get; }

        public ReceiveWindow? Rx1 { get; }
        public ReceiveWindow Rx2 { get; }

        public ReadOnlyMemory<byte> Data { get; }

        public StationEui StationEui { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DownlinkMessage"/> class.
        /// This method is used in case of a response to a upstream message.
        /// </summary>
        /// <returns><see cref="DownlinkMessage"/> object ready to be sent.</returns>
        public DownlinkMessage(byte[] payload,
                               ulong xtime,
                               ReceiveWindow? rx1,
                               ReceiveWindow rx2,
                               DevEui devEui,
                               RxDelay lnsRxDelay,
                               LoRaDeviceClassType deviceClassType,
                               StationEui stationEui = default,
                               uint? antennaPreference = null)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            Data = payload;

            DevEui = devEui;
            LnsRxDelay = lnsRxDelay;
            DeviceClassType = deviceClassType;
            AntennaPreference = antennaPreference;
            Rx1 = rx1;
            Rx2 = rx2;
            StationEui = stationEui;
            Xtime = xtime;
        }
    }
}
