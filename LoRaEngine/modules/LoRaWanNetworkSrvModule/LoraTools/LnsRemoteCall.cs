// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools
{
    public sealed record LnsRemoteCall(RemoteCallKind Kind, string? JsonData);

    public enum RemoteCallKind
    {
        CloudToDeviceMessage,
        ClearCache,
        CloseConnection
    }
}
