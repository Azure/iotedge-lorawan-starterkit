// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Collections.Immutable;

    internal sealed record CupsUpdateInfoRequest
    {
        public CupsUpdateInfoRequest(StationEui stationEui,
                                     Uri? cupsUri,
                                     Uri? tcUri,
                                     uint cupsCredentialsChecksum,
                                     uint tcCredentialsChecksum,
                                     string package,
                                     ImmutableArray<uint> keyChecksums)
        {
            StationEui = stationEui;
            CupsUri = cupsUri;
            TcUri = tcUri;
            CupsCredentialsChecksum = cupsCredentialsChecksum;
            TcCredentialsChecksum = tcCredentialsChecksum;
            Package = package;
            KeyChecksums = keyChecksums;
        }

        public StationEui StationEui { get; }
        public Uri? CupsUri { get; }
        public Uri? TcUri { get; }
        public uint CupsCredentialsChecksum { get; }
        public uint TcCredentialsChecksum { get; }
        public string Package { get; }
        public ImmutableArray<uint> KeyChecksums { get; }
    }
}
