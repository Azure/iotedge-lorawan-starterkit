// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    public enum LoRaDeviceRequestFailedReason
    {
        ApplicationError,
        NotMatchingDeviceByDevAddr,
        NotMatchingDeviceByMicCheck,
        InvalidNetId,
        InvalidRxpk,
        InvalidRegion,
        UnknownDevice,
        InvalidJoinRequest,
        HandledByAnotherGateway,
        BelongsToAnotherGateway,
        JoinDevNonceAlreadyUsed,
        JoinMicCheckFailed,
        ReceiveWindowMissed,
        ConfirmationResubmitThresholdExceeded,
        InvalidFrameCounter,
        IoTHubProblem,
        DeduplicationDrop,
        DeviceClientConnectionFailed
    }
}