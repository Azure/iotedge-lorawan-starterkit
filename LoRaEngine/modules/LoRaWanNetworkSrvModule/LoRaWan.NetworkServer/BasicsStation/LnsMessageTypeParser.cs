// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.ComponentModel.DataAnnotations;

    public static class LnsMessageTypeParser
    {
        internal static LnsMessageType? TryParse(string s) =>
            s switch
            {
                "version" => LnsMessageType.Version,
                "router_config" => LnsMessageType.RouterConfig,
                "jreq" => LnsMessageType.JoinRequest,
                "updf" => LnsMessageType.UplinkDataFrame,
                "dntxed" => LnsMessageType.TransmitConfirmation,
                "dnmsg" => LnsMessageType.DownlinkMessage,
                "propdf" => LnsMessageType.ProprietaryDataFrame,
                "dnsched" => LnsMessageType.MulticastSchedule,
                "timesync" => LnsMessageType.TimeSync,
                "runcmd" => LnsMessageType.RunCommand,
                "rmtsh" => LnsMessageType.RemoteShell,
                _ => null
            };

        internal static LnsMessageType ParseAndValidate(string s, LnsMessageType? expectedType) =>
            TryParse(s) is { } parsedType
               ? expectedType is null || parsedType == expectedType
                   ? parsedType
                   : throw new ValidationException($"Input msgtype parsed as {parsedType}, but was expecting {expectedType}.")
               : throw new FormatException($"Could not parse {s} as a valid {nameof(LnsMessageType)}.");
    }
}
