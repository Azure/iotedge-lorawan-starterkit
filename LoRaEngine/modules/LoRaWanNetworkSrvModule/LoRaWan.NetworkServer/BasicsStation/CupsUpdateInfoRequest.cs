// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;

    internal record CupsUpdateInfoRequest : CupsBase
    {
        public CupsUpdateInfoRequest(StationEui stationEui,
                                     Uri? cupsUri,
                                     Uri? tcUri,
                                     uint cupsCredentialsChecksum,
                                     uint tcCredentialsChecksum)
            : base(cupsUri, tcUri, cupsCredentialsChecksum, tcCredentialsChecksum)
        {
            StationEui = stationEui;
        }

        public StationEui StationEui { get; }
    }
}
