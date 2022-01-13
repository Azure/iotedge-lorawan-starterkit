// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;

    internal sealed record CupsUpdateInfoRequest
    {
        public CupsUpdateInfoRequest(StationEui stationEui,
                                     Uri? cupsUri,
                                     Uri? tcUri,
                                     uint cupsCredentialsChecksum,
                                     uint tcCredentialsChecksum)
        {
            StationEui = stationEui;
            CupsUri = cupsUri;
            TcUri = tcUri;
            CupsCredentialsChecksum = cupsCredentialsChecksum;
            TcCredentialsChecksum = tcCredentialsChecksum;
        }

        public StationEui StationEui { get; }
        public Uri? CupsUri { get; }
        public Uri? TcUri { get; }
        public uint CupsCredentialsChecksum { get; }
        public uint TcCredentialsChecksum { get; }
    }
}
