// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
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
}
