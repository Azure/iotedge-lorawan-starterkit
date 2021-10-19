// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.JsonHandlers
{
    using System;
    using System.Buffers.Binary;
    using System.IO;
    using System.Net.NetworkInformation;
    using System.Text;
    using System.Text.Json;

    public static class Discovery
    {
        /// <summary>
        /// Reads out a StationEui value from a Discovery Query JSON string.
        /// </summary>
        /// <param name="input">The input JSON string.</param>
        /// <param name="stationEui">The StationEui parsed value.</param>
        public static void ReadQuery(string input,
                                     out StationEui stationEui)
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(input));
            _ = reader.Read();
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();
            _ = reader.Read();

            stationEui = default;
            var readStationEui = false;

            while (reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.ValueTextEquals("router"))
                {
                    _ = reader.Read();
#pragma warning disable IDE0010 // All missing cases are handled with NotSupportedException
                    switch (reader.TokenType)
#pragma warning restore IDE0010 // All missing cases are handled with NotSupportedException
                    {
                        case JsonTokenType.Number:
                        {
                            var v = reader.GetUInt64();
                            stationEui = new StationEui(v);
                            break;
                        }
                        case JsonTokenType.String:
                        {
                            var s = reader.GetString();
                            stationEui = s.Contains(':', StringComparison.Ordinal)
                                       ? Id6.TryParse(s, out var id6) ? new StationEui(id6) : throw new JsonException()
                                       : Hexadecimal.TryParse(s, out var hhd, '-') ? new StationEui(hhd) : throw new JsonException();
                            break;
                        }
                        default:
                            throw new NotSupportedException("'router' field should be either a number or a string.");
                    }
                    readStationEui = true;
                    _ = reader.Read();
                }
                else
                {
                    _ = reader.Read();
                    reader.Skip();
                    _ = reader.Read();
                }
            }

            if (!readStationEui)
                throw new JsonException("Missing required property 'router' in input JSON.");
        }

        /// <summary>
        /// Serializes the response for Discovery endpoint as a JSON string.
        /// </summary>
        /// <param name="station">The <see cref="StationEui"/> of the querying basic station.</param>
        /// <param name="muxs">The identity of the LNS Data endpoint (<see cref="Id6"/> formatted).</param>
        /// <param name="uri">The URI of the LNS Data endpoint.</param>
        /// <param name="error">The error message. If not <see langword="null"/>, the Basic Station will retry discovery.</param>
        /// <returns>The JSON string to be sent as Discovery endpoint response.</returns>
        public static string SerializeResponse(StationEui station,
                                               string muxs,
                                               Uri uri,
                                               string error)
        {
            if (!Id6.TryParse(muxs, out var _)) throw new ArgumentException("Argument should be a valid string in ID6 format.", nameof(muxs));
            if (uri is null) throw new ArgumentNullException(nameof(uri));

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            InternalWriteResponse(writer, station, muxs, uri, error);
            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private static void InternalWriteResponse(Utf8JsonWriter writer, StationEui station, string lnsEndpoint, Uri lnsUri, string error)
        {
            writer.WriteStartObject();

            writer.WriteString("router", Id6.Format(station.AsUInt64(), Id6.FormatOptions.Lowercase));
            writer.WriteString("muxs", lnsEndpoint);
            writer.WriteString("uri", lnsUri.ToString());
            if (!string.IsNullOrEmpty(error))
                writer.WriteString("error", error);

            writer.WriteEndObject();
        }

        /// <summary>
        /// Gets an ID6 compatible representation of the provided network interface MAC Address.
        /// </summary>
        /// <param name="networkInterface">The network interface for which the ID6 MAC Address representation should be extracted.</param>
        /// <returns></returns>
        internal static string GetMacAddressAsID6(NetworkInterface networkInterface)
        {
            var physicalAddress = 0UL;

            if (networkInterface is not null)
            {
                // As per specification (https://doc.sm.tc/station/glossary.html?highlight=id6)
                // for an ID6 based on a MAC Address we expect FFFE in the middle
                var physicalAddress48 = networkInterface.GetPhysicalAddress().GetAddressBytes();

                Span<byte> physicalAddress64 = stackalloc byte[8];
                physicalAddress48[..3].CopyTo(physicalAddress64);
                physicalAddress64[3] = 0xFF;
                physicalAddress64[4] = 0xFE;
                physicalAddress48[3..].CopyTo(physicalAddress64[5..]);
                physicalAddress = BinaryPrimitives.ReadUInt64BigEndian(physicalAddress64);
            }

            return Id6.Format(physicalAddress, Id6.FormatOptions.FixedWidth);
        }
    }
}
