// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Text.Json;

    public static class LnsDiscovery
    {
        internal static readonly IJsonReader<StationEui> QueryReader =
            JsonReader.Object(
                JsonReader.Property("router",
                    JsonReader.Either(from n in JsonReader.UInt64()
                                      select new StationEui(n),
                                      from s in JsonReader.String()
                                      select s.Contains(':', StringComparison.Ordinal)
                                           ? Id6.TryParse(s, out var id6) ? new StationEui(id6) : throw new JsonException()
                                           : Hexadecimal.TryParse(s, out ulong hhd, '-') ? new StationEui(hhd) : throw new JsonException())));

        /// <summary>
        /// Writes the response for Discovery endpoint as a JSON string.
        /// </summary>
        /// <param name="writer">The write to use for serialization.</param>
        /// <param name="router">The <see cref="StationEui"/> of the querying basic station.</param>
        /// <param name="muxs">The identity of the LNS Data endpoint (<see cref="Id6"/> formatted).</param>
        /// <param name="url">The URI of the LNS Data endpoint.</param>
        public static void WriteResponse(Utf8JsonWriter writer, StationEui router, string muxs, Uri url)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (!Id6.TryParse(muxs, out _)) throw new ArgumentException("Argument should be a string in ID6 format.", nameof(muxs));
            if (url is null) throw new ArgumentNullException(nameof(url));

            writer.WriteStartObject();
            writer.WriteString("router", Id6.Format(router.AsUInt64, Id6.FormatOptions.Lowercase));
            writer.WriteString("muxs", muxs);
            writer.WriteString("uri", url.ToString());
            writer.WriteEndObject();
        }

        public static void WriteResponse(Utf8JsonWriter writer, StationEui router, string error)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WriteStartObject();
            writer.WriteString("router", Id6.Format(router.AsUInt64, Id6.FormatOptions.Lowercase));
            writer.WriteString("error", error);
            writer.WriteEndObject();
        }
    }
}
