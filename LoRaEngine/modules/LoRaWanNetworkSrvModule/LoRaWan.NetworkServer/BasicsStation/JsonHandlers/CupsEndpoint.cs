// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Collections.Immutable;
    using Jacob;

    public static class CupsEndpoint
    {
        internal static readonly IJsonReader<CupsUpdateInfoRequest> UpdateRequestReader =
            JsonReader.Object(JsonReader.Property("router", from s in JsonReader.String()
                                                            select StationEui.Parse(s)),
                              JsonReader.Property("cupsUri", from s in JsonReader.String().OrNull()
                                                             select string.IsNullOrEmpty(s) ? null : new Uri(s)),
                              JsonReader.Property("tcUri", from s in JsonReader.String().OrNull()
                                                           select string.IsNullOrEmpty(s) ? null : new Uri(s)),
                              JsonReader.Property("cupsCredCrc", JsonReader.UInt32()),
                              JsonReader.Property("tcCredCrc", JsonReader.UInt32()),
                              JsonReader.Property("package", from s in JsonReader.String().OrNull()
                                                             select string.IsNullOrEmpty(s) ? null : s),
                              JsonReader.Property("keys", JsonReader.Array(JsonReader.UInt32())),
                              (r, c, t, cc, tc, p, k) => new CupsUpdateInfoRequest(r, c, t, cc, tc, p, k.ToImmutableArray()));
    }
}
