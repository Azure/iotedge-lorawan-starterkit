// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public class RadioMetadata
    {
        public DataRate DataRate { get; }
        public Hertz Frequency { get; }
        public RadioMetadataUpInfo UpInfo { get; }

        public RadioMetadata(DataRate dataRate, Hertz frequency, RadioMetadataUpInfo upInfo)
        {
            DataRate = dataRate;
            Frequency = frequency;
            UpInfo = upInfo;
        }
    }

    public class RadioMetadataUpInfo
    {
        public RadioMetadataUpInfo(uint antennaPreference, ulong xtime, uint gpsTime, double receivedSignalStrengthIndication, float signalNoiseRatio)
        {
            AntennaPreference = antennaPreference;
            Xtime = xtime;
            GpsTime = gpsTime;
            ReceivedSignalStrengthIndication = receivedSignalStrengthIndication;
            SignalNoiseRatio = signalNoiseRatio;
        }

        public uint AntennaPreference { get; }
        public ulong Xtime { get; }
        public uint GpsTime { get; }
        public double ReceivedSignalStrengthIndication { get; }
        public float SignalNoiseRatio { get; }
    }
}
