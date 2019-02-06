// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.LoRaPhysical;

    public class LoRaDeviceRequestProcessResult
    {
        public LoRaDeviceRequestProcessResult(LoRaDevice loRaDevice, LoRaRequest request, DownlinkPktFwdMessage downlinkMessage = null)
        {
            this.LoRaDevice = loRaDevice;
            this.Request = request;
            this.DownlinkMessage = downlinkMessage;
        }

        public LoRaDeviceRequestProcessResult(LoRaDevice loRaDevice, LoRaRequest request, LoRaDeviceRequestFailedReason failedReason)
        {
            this.LoRaDevice = loRaDevice;
            this.Request = request;
            this.FailedReason = failedReason;
        }

        public DownlinkPktFwdMessage DownlinkMessage { get; }

        public LoRaRequest Request { get; }

        public LoRaDevice LoRaDevice { get; }

        public LoRaDeviceRequestFailedReason? FailedReason { get; }
    }
}