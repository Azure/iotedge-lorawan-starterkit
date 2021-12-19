// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    public record RadioMetadata(DataRateIndex DataRate, Hertz Frequency, RadioMetadataUpInfo UpInfo);

    public record RadioMetadataUpInfo(uint AntennaPreference, ulong Xtime, uint GpsTime, double ReceivedSignalStrengthIndication, float SignalNoiseRatio);
}
