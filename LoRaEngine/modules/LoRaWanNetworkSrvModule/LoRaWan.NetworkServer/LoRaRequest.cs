// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer.BasicsStation;

    public class LoRaRequest
    {
        public virtual StationEui StationEui { get; private set; }

        public virtual RadioMetadata RadioMetadata { get; }

        public virtual LoRaPayload Payload { get; private set; }

        public virtual IDownstreamMessageSender DownstreamMessageSender { get; }

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
            RadioMetadata radioMetadata,
            IDownstreamMessageSender downstreamMessageSender,
            DateTime startTime)
        {
            RadioMetadata = radioMetadata;
            DownstreamMessageSender = downstreamMessageSender;
            StartTime = startTime;
        }

        internal void NotifyFailed(LoRaDevice loRaDevice, Exception error) => NotifyFailed(loRaDevice, LoRaDeviceRequestFailedReason.ApplicationError, error);

        internal void NotifyFailed(LoRaDevice loRaDevice, LoRaDeviceRequestFailedReason reason, Exception exception = null) => NotifyFailed(loRaDevice?.DevEUI.ToString(), reason, exception);

        internal virtual void NotifyFailed(LoRaDeviceRequestFailedReason reason, Exception exception = null) => NotifyFailed((string)null, reason, exception);

        public virtual void NotifyFailed(string deviceId, LoRaDeviceRequestFailedReason reason, Exception exception = null)
        {
        }

        public virtual void NotifySucceeded(LoRaDevice loRaDevice, DownlinkMessage downlink)
        {
        }

        internal void SetPayload(LoRaPayload loRaPayload) => Payload = loRaPayload;

        internal void SetRegion(Region loRaRegion) => Region = loRaRegion;

        internal void SetStationEui(StationEui stationEui) => StationEui = stationEui;

        public virtual LoRaOperationTimeWatcher GetTimeWatcher() => new LoRaOperationTimeWatcher(Region, StartTime);
    }
}
