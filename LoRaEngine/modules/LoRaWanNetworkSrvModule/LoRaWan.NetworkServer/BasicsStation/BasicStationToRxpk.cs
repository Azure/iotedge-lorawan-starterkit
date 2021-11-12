// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Globalization;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;

    public class BasicStationToRxpk : Rxpk
    {
        public BasicStationToRxpk(RadioMetadata radioMetadata, Region region)
        {
            if (radioMetadata is null) throw new ArgumentNullException(nameof(radioMetadata));
            if (region is null) throw new ArgumentNullException(nameof(region));

            Freq = radioMetadata.Frequency.Mega;
            Datr = region.DRtoConfiguration[(ushort)radioMetadata.DataRate.AsInt32].configuration;
            Rssi = Convert.ToInt32(radioMetadata.UpInfo.ReceivedSignalStrengthIndication);
            Lsnr = radioMetadata.UpInfo.SignalNoiseRatio;
            Rfch = radioMetadata.UpInfo.AntennaPreference;
            Time = radioMetadata.UpInfo.Xtime.ToString(CultureInfo.InvariantCulture); // This field was just used for telemetry in rxpk. Now it's being used for bringing unaltered the Xtime.

            Tmst = unchecked((uint)radioMetadata.UpInfo.Xtime); // This is used by former computation only. Should go away when we drop PktFwd support.
            Chan = unchecked((uint)radioMetadata.DataRate.AsInt32); // This is not used in any computation. It is only reported in the device telemetry.
            Stat = 1; // This is not used in any computation. It is only reported in the device telemetry.
            Tmms = radioMetadata.UpInfo.GpsTime; // This is not used in any computation. It is only reported in the device telemetry.
            Codr = "4/5"; // This is not used in any computation. It is only reported in the device telemetry.
            Modu = "LORA"; // This is only used in test path by legacy PacketForwarder code. Safe to eventually remove. Could be also "FSK"
            Size = 0; // This is only used in test path by legacy PacketForwarder code. Safe to eventually remove.
            Data = "REFUQQ=="; // This base64 encoded string is only used in test path by legacy PacketForwarder code. Safe to eventually remove.
        }
    }
}
