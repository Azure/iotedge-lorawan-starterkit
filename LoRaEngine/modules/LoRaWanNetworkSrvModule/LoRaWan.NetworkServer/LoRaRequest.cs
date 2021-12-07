// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;

    public class LoRaRequest
    {
        public virtual StationEui StationEui { get; private set; }

        public virtual Rxpk Rxpk { get; }

        public virtual LoRaPayload Payload { get; private set; }

        public virtual IPacketForwarder PacketForwarder { get; }

        public virtual DateTime StartTime { get; }

        public virtual Region Region { get; private set; }

        protected LoRaRequest()
        {
        }

        protected LoRaRequest(LoRaPayload payload)
        {
            Payload = payload;
        }

        public LoRaRequest(
            Rxpk rxpk,
            IPacketForwarder packetForwarder,
            DateTime startTime)
        {
            Rxpk = rxpk;
            PacketForwarder = packetForwarder;
            StartTime = startTime;
        }

        internal void NotifyFailed(LoRaDevice loRaDevice, Exception error) => NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ApplicationError, error);

        internal void NotifyFailed(LoRaDevice loRaDevice, LoRaDeviceRequestFailedReason reason, Exception exception = null) => NotifyFailed(loRaDevice?.DevEUI, reason, exception);

        internal void NotifyFailed(LoRaDeviceRequestFailedReason reason, Exception exception = null) => NotifyFailed((string)null, reason, exception);

        public virtual void NotifyFailed(string deviceId, LoRaDeviceRequestFailedReason reason, Exception exception = null)
        {
        }

        public virtual void NotifySucceeded(LoRaDevice loRaDevice, DownlinkPktFwdMessage downlink)
        {
        }

        internal void SetPayload(LoRaPayload loRaPayload) => Payload = loRaPayload;

        internal void SetRegion(Region loRaRegion) => Region = loRaRegion;

        internal void SetStationEui(StationEui stationEui) => StationEui = stationEui;

        public virtual LoRaOperationTimeWatcher GetTimeWatcher() => new LoRaOperationTimeWatcher(Region, StartTime);
    }
}
