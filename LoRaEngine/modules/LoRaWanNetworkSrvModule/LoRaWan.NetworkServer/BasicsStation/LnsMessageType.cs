// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Runtime.CompilerServices;
    internal enum LnsMessageType
    {
        Version,                // version
        RouterConfig,           // router_config
        JoinRequest,            // jreq
        UplinkDataFrame,        // updf
        TransmitConfirmation,   // dntxed
        DownlinkMessage,        // dnmsg

        /* Following are not implemented:

        ProprietaryDataFrame,   // propdf
        MulticastSchedule,      // dnsched
        TimeSync,               // timesync
        RunCommand,             // runcmd
        RemoteShell,            // rmtsh

        */
    }

    internal static class LnsMessageTypeExtensions
    {
        internal static bool TryParseLnsMessageType(string s, out LnsMessageType? lnsMessageType)
        {
            lnsMessageType = s switch
            {
                "version" => LnsMessageType.Version,
                "router_config" => LnsMessageType.RouterConfig,
                "jreq" => LnsMessageType.JoinRequest,
                "updf" => LnsMessageType.UplinkDataFrame,
                "dntxed" => LnsMessageType.TransmitConfirmation,
                "dnmsg" => LnsMessageType.DownlinkMessage,
                _ => null
            };
            return lnsMessageType is not null;
        }

        internal static LnsMessageType ParseAndValidate(string s, LnsMessageType? expectedType) =>
            TryParseLnsMessageType(s, out var parsedType)
               ? expectedType is null ^ parsedType == expectedType
                   ? parsedType.Value
                   : throw new InvalidOperationException($"Input msgtype parsed as {parsedType}, but was expecting {expectedType}.")
               : throw new ArgumentException($"Could not parse {s} as a valid {nameof(LnsMessageType)}.");

        internal static string ToString(this LnsMessageType lnsMessageType) => lnsMessageType switch
        {
            LnsMessageType.Version              => "version",
            LnsMessageType.RouterConfig         => "router_config",
            LnsMessageType.JoinRequest          => "jreq",
            LnsMessageType.UplinkDataFrame      => "updf",
            LnsMessageType.TransmitConfirmation => "dntxed",
            LnsMessageType.DownlinkMessage      => "dnmsg",
            _ => throw new SwitchExpressionException(lnsMessageType)
        };
    }
}
