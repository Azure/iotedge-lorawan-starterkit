// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.LoRaPhysical
{
    using System;
    using System.Globalization;
    using LoRaTools.Regions;
    using LoRaWan;
    using Newtonsoft.Json;

    /// <summary>
    /// JSON of a Downlink message for the Packet forwarder.
    /// </summary>
    public class DownlinkPktFwdMessage
    {
        [JsonProperty("txpk")]
        public Txpk Txpk { get; set; }

        [JsonIgnore]
        public string DevEui { get; }

        [JsonIgnore]
        public ushort LnsRxDelay { get; }

        [JsonIgnore]
        public ulong Xtime { get; }

        [JsonIgnore]
        public uint? AntennaPreference { get; }

        [JsonIgnore]
        public StationEui StationEui { get; }

        [JsonIgnore]
        public DeviceJoinInfo DeviceJoinInfo { get; set; }

        public DownlinkPktFwdMessage()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DownlinkPktFwdMessage"/> class.
        /// This method is used in case of a response to a upstream message.
        /// </summary>
        /// <returns>DownlinkPktFwdMessage object ready to be sent.</returns>
        public DownlinkPktFwdMessage(byte[] loRaData, string datr, double freq, string devEui, long tmst = 0, ushort lnsRxDelay = 0, uint? rfch = null, string time = "", StationEui stationEui = default, DeviceJoinInfo deviceJoinInfo = null)
        {
            if (loRaData is null) throw new ArgumentNullException(nameof(loRaData));

            Txpk = new Txpk()
            {
                Imme = tmst == 0,
                Tmst = tmst,
                Data = Convert.ToBase64String(loRaData),
                Size = (uint)loRaData.Length,
                Freq = freq,
                Rfch = 0,
                Modu = "LORA",
                Datr = datr,
                Codr = "4/5",
                // TODO put 14 for EU
                Powe = 14,
                Ipol = true
            };

            DevEui = devEui;
            LnsRxDelay = lnsRxDelay;
            AntennaPreference = rfch;
            StationEui = stationEui;
            Xtime = string.IsNullOrEmpty(time) ? 0 : ulong.Parse(time, CultureInfo.InvariantCulture);
            DeviceJoinInfo = deviceJoinInfo;
        }
    }
}
