// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;

    public static class CupsEndpoint
    {
        internal static class CupsBaseProperties
        {
            internal static readonly IJsonProperty<Uri> CupsUri =
                JsonReader.Property("cupsUri", from s in JsonReader.Either(JsonReader.String(), JsonReader.Null<string>())
                                               select string.IsNullOrEmpty(s) ? null : new Uri(s));

            internal static readonly IJsonProperty<Uri?> TcUri =
                JsonReader.Property("tcUri", from s in JsonReader.Either(JsonReader.String(), JsonReader.Null<string>())
                                             select string.IsNullOrEmpty(s) ? null : new Uri(s));

            internal static readonly IJsonProperty<uint> CupsCredCrc =
                JsonReader.Property("cupsCredCrc", JsonReader.UInt32());

            internal static readonly IJsonProperty<uint> TcCredCrc =
                JsonReader.Property("tcCredCrc", JsonReader.UInt32());
        }

        internal static readonly IJsonReader<CupsUpdateInfoRequest> UpdateRequestReader =
            JsonReader.Object(JsonReader.Property("router", from s in JsonReader.String()
                                                            select StationEui.Parse(s)),
                              CupsBaseProperties.CupsUri,
                              CupsBaseProperties.TcUri,
                              CupsBaseProperties.CupsCredCrc,
                              CupsBaseProperties.TcCredCrc,
                              (r, c, t, cc, tc) => new CupsUpdateInfoRequest(r, c, t, cc, tc));

        internal static readonly IJsonReader<CupsTwinInfo> TwinReader =
            JsonReader.Object(CupsBaseProperties.CupsUri,
                              CupsBaseProperties.TcUri,
                              CupsBaseProperties.CupsCredCrc,
                              CupsBaseProperties.TcCredCrc,
                              (c, t, cc, tc) => new CupsTwinInfo(c, t, cc, tc));
    }
}
