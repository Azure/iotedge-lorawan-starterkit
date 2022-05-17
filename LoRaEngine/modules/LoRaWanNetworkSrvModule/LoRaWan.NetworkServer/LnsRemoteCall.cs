// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    internal sealed record LnsRemoteCall(RemoteCallKind Kind, string? JsonData);

    internal enum RemoteCallKind
    {
        CloudToDeviceMessage,
        ClearCache,
        CloseConnection
    }
}
