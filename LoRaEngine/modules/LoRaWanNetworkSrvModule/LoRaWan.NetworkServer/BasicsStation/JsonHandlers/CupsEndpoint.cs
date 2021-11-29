// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;

    public static class CupsEndpoint
    {
        /*
         * {
          "router"      : ID6,
          "cupsUri"     : "URI",
          "tcUri"       : "URI",
          "cupsCredCrc" : INT,
          "tcCredCrc"   : INT,
          "station"     : STRING,
          "model"       : STRING,
          "package"     : STRING,
          "keys"        : [INT]
        }*/

        internal static class CommonCupsProperties
        {
            internal static readonly IJsonProperty<Uri> CupsUri =
                JsonReader.Property("cupsUri", from s in JsonReader.String()
                                               select new Uri(s));

            internal static readonly IJsonProperty<Uri?> TcUri =
                JsonReader.Property("tcUri", from s in JsonReader.Either(JsonReader.String(), JsonReader.Null<string>())
                                             select string.IsNullOrEmpty(s) ? null : new Uri(s));

            internal static readonly IJsonProperty<uint> CupsCredCrc =
                JsonReader.Property("cupsCredCrc", JsonReader.UInt32());

            internal static readonly IJsonProperty<uint> TcCredCrc =
                JsonReader.Property("tcCredCrc", JsonReader.UInt32());
        }

        internal static readonly IJsonReader<CupsUpdateRequest> UpdateRequestReader =
            //TBD if to split or not
            JsonReader.Object(JsonReader.Property("router", from s in JsonReader.Either(JsonReader.String(), JsonReader.Null<string>())
                                                            select string.IsNullOrEmpty(s) ? (StationEui?)null : StationEui.Parse(s)),
                              CommonCupsProperties.CupsUri,
                              CommonCupsProperties.TcUri,
                              CommonCupsProperties.CupsCredCrc,
                              CommonCupsProperties.TcCredCrc,
                              (r, c, t, cc, tc) => new CupsUpdateRequest(r, c, t, cc, tc));
    }
}
