// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;

    internal enum LnsMessageType
    {
        Version,                // version
        RouterConfig,           // router_config
        JoinRequest,            // jreq
        UplinkDataFrame,        // updf
        TransmitConfirmation,   // dntxed
        DownlinkMessage,        // dnmsg

        // Following message types are not handled in current LoRaWan Network Server implementation
        ProprietaryDataFrame,   // propdf
        MulticastSchedule,      // dnsched
        TimeSync,               // timesync
        RunCommand,             // runcmd
        RemoteShell             // rmtsh
    }

    internal static class LnsMessageTypeExtensions
    {
        internal static string ToBasicStationString(this LnsMessageType lnsMessageType) => lnsMessageType switch
        {
#pragma warning disable format
            LnsMessageType.Version => "version",
            LnsMessageType.RouterConfig => "router_config",
            LnsMessageType.JoinRequest => "jreq",
            LnsMessageType.UplinkDataFrame => "updf",
            LnsMessageType.TransmitConfirmation => "dntxed",
            LnsMessageType.DownlinkMessage => "dnmsg",
            LnsMessageType.ProprietaryDataFrame => "propdf",
            LnsMessageType.MulticastSchedule => "dnsched",
            LnsMessageType.TimeSync => "timesync",
            LnsMessageType.RunCommand => "runcmd",
            LnsMessageType.RemoteShell => "rmtsh",
#pragma warning restore format
            _ => throw new ArgumentOutOfRangeException(nameof(lnsMessageType), lnsMessageType, null),
        };
    }
}
