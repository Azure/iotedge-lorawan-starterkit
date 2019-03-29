// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    /// <summary>
    /// Call durations, all in ms
    /// </summary>
    public class ParallelTestConfiguration
    {
        public string GatewayID { get; set; }

        public uint? DeviceID { get; set; }

        public RecordedDuration BetweenMessageDuration { get; set; }

        public RecordedDuration SendEventDuration { get; set; }

        public RecordedDuration ReceiveEventDuration { get; set; }

        public RecordedDuration UpdateTwinDuration { get; set; }

        public RecordedDuration LoadTwinDuration { get; set; }

        public RecordedDuration DeviceApiResetFcntDuration { get; set; }

        public RecordedDuration SearchByDevAddrDuration { get; set; }

        public uint? DeviceTwinFcntUp { get; set; }

        public uint? DeviceTwinFcntDown { get; set; }
    }
}