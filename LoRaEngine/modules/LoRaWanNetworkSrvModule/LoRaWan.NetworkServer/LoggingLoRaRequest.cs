// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Composition of a <see cref="LoRaRequest"/> that logs at the end of the process
    /// </summary>
    public class LoggingLoRaRequest : LoRaRequest
    {
        private readonly LoRaRequest wrappedRequest;

        public override IPacketForwarder PacketForwarder => this.wrappedRequest.PacketForwarder;

        public override Region Region => this.wrappedRequest.Region;

        public override LoRaPayload Payload => this.wrappedRequest.Payload;

        public override Rxpk Rxpk => this.wrappedRequest.Rxpk;

        public override DateTime StartTime => this.wrappedRequest.StartTime;

        public LoggingLoRaRequest(LoRaRequest wrappedRequest)
        {
            this.wrappedRequest = wrappedRequest;
        }

        public override void NotifyFailed(string deviceId, LoRaDeviceRequestFailedReason reason, Exception exception = null)
        {
            this.wrappedRequest.NotifyFailed(deviceId, reason, exception);
            this.LogProcessingTime(deviceId);
        }

        public override void NotifySucceeded(LoRaDevice loRaDevice, DownlinkPktFwdMessage downlink)
        {
            this.wrappedRequest.NotifySucceeded(loRaDevice, downlink);
            this.LogProcessingTime(loRaDevice?.DevEUI);
        }

        private void LogProcessingTime(string deviceId)
        {
            deviceId = deviceId ?? ConversionHelper.ByteArrayToString(this.wrappedRequest.Payload.DevAddr);
            Logger.Log(deviceId, $"processing time: {DateTime.UtcNow.Subtract(this.wrappedRequest.StartTime)}", LogLevel.Debug);
        }

        public override LoRaOperationTimeWatcher GetTimeWatcher() => this.wrappedRequest.GetTimeWatcher();
    }
}