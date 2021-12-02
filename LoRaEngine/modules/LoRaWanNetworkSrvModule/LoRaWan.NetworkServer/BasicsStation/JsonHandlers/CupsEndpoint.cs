// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;

    public static class CupsEndpoint
    {
        internal static readonly IJsonReader<CupsUpdateInfoRequest> UpdateRequestReader =
            JsonReader.Object(JsonReader.Property("router", from s in JsonReader.String()
                                                            select StationEui.Parse(s)),
                              JsonReader.Property("cupsUri", from s in JsonReader.Either(JsonReader.String(), JsonReader.Null<string>())
                                                             select string.IsNullOrEmpty(s) ? null : new Uri(s)),
                              JsonReader.Property("tcUri", from s in JsonReader.Either(JsonReader.String(), JsonReader.Null<string>())
                                                           select string.IsNullOrEmpty(s) ? null : new Uri(s)),
                              JsonReader.Property("cupsCredCrc", JsonReader.UInt32()),
                              JsonReader.Property("tcCredCrc", JsonReader.UInt32()),
                              (r, c, t, cc, tc) => new CupsUpdateInfoRequest(r, c, t, cc, tc));
    }
}
