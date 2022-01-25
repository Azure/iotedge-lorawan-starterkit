// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public sealed record RadioMetadata
    {
        public RadioMetadata(DataRateIndex dataRate, Hertz frequency, RadioMetadataUpInfo upInfo)
        {
            DataRate = dataRate;
            Frequency = frequency;
            UpInfo = upInfo;
        }

        public DataRateIndex DataRate { get; init; }
        public Hertz Frequency { get; init; }
        public RadioMetadataUpInfo UpInfo { get; init; }
    }

    public sealed record RadioMetadataUpInfo
    {
        public RadioMetadataUpInfo(uint antennaPreference, ulong xtime, uint gpsTime,
                                   double receivedSignalStrengthIndication, float signalNoiseRatio)
        {
            AntennaPreference = antennaPreference;
            Xtime = xtime;
            GpsTime = gpsTime;
            ReceivedSignalStrengthIndication = receivedSignalStrengthIndication;
            SignalNoiseRatio = signalNoiseRatio;
        }

        // Following field is corresponding to the rctx field in LNS protocol
        public uint AntennaPreference { get; init; }
        public ulong Xtime { get; init; }
        public uint GpsTime { get; init; }
        public double ReceivedSignalStrengthIndication { get; init; }
        public float SignalNoiseRatio { get; init; }
    }
}
