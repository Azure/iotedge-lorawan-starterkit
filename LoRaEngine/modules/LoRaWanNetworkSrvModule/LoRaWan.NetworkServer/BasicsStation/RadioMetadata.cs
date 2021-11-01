// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public class RadioMetadata
    {
        public DataRate DataRate { get; }
        public Hertz Frequency { get; }
        public uint AntennaPreference { get; }
        public ulong Xtime { get; }
        public uint GpsTime { get; }
        public double ReceivedSignalStrengthIndication { get; }
        public float SignalNoiseRatio { get; }

        public RadioMetadata(DataRate dataRate, Hertz frequency, uint antennaPreference, ulong xtime, uint gpsTime, double receivedSignalStrengthIndication, float signalNoiseRatio)
        {
            DataRate = dataRate;
            Frequency = frequency;
            AntennaPreference = antennaPreference;
            Xtime = xtime;
            GpsTime = gpsTime;
            ReceivedSignalStrengthIndication = receivedSignalStrengthIndication;
            SignalNoiseRatio = signalNoiseRatio;
        }
    }
}
