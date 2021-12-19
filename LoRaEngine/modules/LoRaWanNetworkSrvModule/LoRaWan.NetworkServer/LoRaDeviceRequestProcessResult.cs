// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.LoRaPhysical;

    public class LoRaDeviceRequestProcessResult
    {
        public LoRaDeviceRequestProcessResult(LoRaDevice loRaDevice, LoRaRequest request, DownlinkBasicsStationMessage downlinkMessage = null)
        {
            LoRaDevice = loRaDevice;
            Request = request;
            DownlinkMessage = downlinkMessage;
        }

        public LoRaDeviceRequestProcessResult(LoRaDevice loRaDevice, LoRaRequest request, LoRaDeviceRequestFailedReason failedReason)
        {
            LoRaDevice = loRaDevice;
            Request = request;
            FailedReason = failedReason;
        }

        public DownlinkBasicsStationMessage DownlinkMessage { get; }

        public LoRaRequest Request { get; }

        public LoRaDevice LoRaDevice { get; }

        public LoRaDeviceRequestFailedReason? FailedReason { get; }
    }
}
