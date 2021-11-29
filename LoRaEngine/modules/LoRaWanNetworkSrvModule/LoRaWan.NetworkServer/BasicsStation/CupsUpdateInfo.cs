// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;

    internal class CupsUpdateInfo
    {
        /* 
          {
            "router"      : ID6,
            "cupsUri"     : "URI",
            "tcUri"       : "URI",
            "cupsCredCrc" : INT,
            "tcCredCrc"   : INT,
            "station"     : STRING,
            "model"       : STRING,
            "package"     : STRING,
            "keys"        : [INT]
          }
        */
        public CupsUpdateInfo(StationEui? stationEui,
                              Uri cupsUri,
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

        public StationEui? StationEui { get; }
        public Uri CupsUri { get; }
        public Uri? TcUri { get; }
        public uint CupsCredentialsChecksum { get; }
        public uint TcCredentialsChecksum { get; }
    }
}
