// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;

    internal sealed record CupsTwinInfo
    {
        // This class is on purpose left equal to CupsBase.
        // Credential management does not require anything more than the shared endpoint URIs and CRCs
        // Firmware management features could require to define fields in twin in a different way than the station/model/package ones
        public CupsTwinInfo(Uri cupsUri,
                            Uri tcUri,
                            uint cupsCredentialsChecksum,
                            uint tcCredentialsChecksum)
        {
            CupsUri = cupsUri;
            TcUri = tcUri;
            CupsCredentialsChecksum = cupsCredentialsChecksum;
            TcCredentialsChecksum = tcCredentialsChecksum;
        }

        public Uri CupsUri { get; }
        public Uri TcUri { get; }
        public uint CupsCredentialsChecksum { get; }
        public uint TcCredentialsChecksum { get; }
    }
}
