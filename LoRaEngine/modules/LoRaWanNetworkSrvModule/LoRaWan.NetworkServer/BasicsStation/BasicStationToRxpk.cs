// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using System;
    using System.Globalization;

    public class BasicStationToRxpk : Rxpk
    {
        /*
           rxpk fields defined here: https://github.com/Lora-net/packet_forwarder/blob/d0226eae6e7b6bbaec6117d0d2372bf17819c438/PROTOCOL.TXT#L131-L147
         */
        public BasicStationToRxpk(RadioMetadata radioMetadata, Region region)
        {
            if (radioMetadata is null) throw new ArgumentNullException(nameof(radioMetadata));
            if (region is null) throw new ArgumentNullException(nameof(region));
            
            Tmst = (uint)(radioMetadata.UpInfo.Xtime & 0xFFFFFFFF); // https://github.com/lorabasics/basicstation/blob/bd17e53ab1137de6abb5ae48d6f3d52f6c268299/examples/station2pkfwd/pkfwdc.py#L145
            Freq = radioMetadata.Frequency.Mega;
            Datr = region.DRtoConfiguration[(ushort)radioMetadata.DataRate.AsInt32].configuration;
            Rssi = Convert.ToInt32(radioMetadata.UpInfo.ReceivedSignalStrengthIndication);
            Lsnr = radioMetadata.UpInfo.SignalNoiseRatio;

            Rfch = radioMetadata.UpInfo.AntennaPreference; // This is not used in any computation. It is only reported in the device telemetry.
            Chan = (uint)radioMetadata.DataRate.AsInt32; // This is not used in any computation. It is only reported in the device telemetry.
            Stat = 1; // This is not used in any computation. It is only reported in the device telemetry.
            Tmms = radioMetadata.UpInfo.GpsTime; // This is not used in any computation. It is only reported in the device telemetry.
            Time = radioMetadata.UpInfo.Xtime.ToString(CultureInfo.InvariantCulture); // This is not used in any computation. It is only reported in the device telemetry.
            Codr = "4/5"; // This is not used in any computation. It is only reported in the device telemetry.
            Modu = "LORA"; // This is only used in test path by legacy PacketForwarder code. Safe to eventually remove. Could be also "FSK"
            Size = 0; // This is only used in test path by legacy PacketForwarder code. Safe to eventually remove.
            Data = "REFUQQ=="; // This base64 encoded string is only used in test path by legacy PacketForwarder code. Safe to eventually remove.
        }
    }
}
