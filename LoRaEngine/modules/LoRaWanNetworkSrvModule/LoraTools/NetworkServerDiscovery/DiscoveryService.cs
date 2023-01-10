// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.NetworkServerDiscovery
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Net.WebSockets;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Jacob;
    using LoRaTools;
    using LoRaWan;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;

    public sealed class DiscoveryService
    {
        private const string DataEndpointPath = "router-data";
        private readonly ILnsDiscovery lnsDiscovery;
        private readonly ILogger<DiscoveryService> logger;

        internal static readonly IJsonReader<StationEui> QueryReader =
            JsonReader.Object(
                JsonReader.Property("router",
                    JsonReader.Either(from n in JsonReader.UInt64()
                                      select new StationEui(n),
                                      from s in JsonReader.String()
                                      select s.Contains(':', StringComparison.Ordinal)
                                           ? Id6.TryParse(s, out var id6) ? new StationEui(id6) : throw new JsonException()
                                           : Hexadecimal.TryParse(s, out ulong hhd, '-') ? new StationEui(hhd) : throw new JsonException())));

        public DiscoveryService(ILnsDiscovery lnsDiscovery, ILogger<DiscoveryService> logger)
        {
            this.lnsDiscovery = lnsDiscovery;
            this.logger = logger;
        }

        public async Task HandleDiscoveryRequestAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            if (httpContext is null) throw new ArgumentNullException(nameof(httpContext));

            var webSocketConnection = new WebSocketConnection(httpContext, this.logger);
            _ = await webSocketConnection.HandleAsync(async (ctx, s, ct) =>
            {
                await using var message = s.ReadTextMessages(cancellationToken);
                if (!await message.MoveNextAsync())
                {
                    this.logger.LogWarning("Did not receive discovery request from station.");
                }
                else
                {
                    var json = message.Current;
                    var stationEui = QueryReader.Read(json);

                    using var scope = this.logger.BeginEuiScope(stationEui);
                    this.logger.LogInformation("Received discovery request from: {StationEui}", stationEui);

                    try
                    {
                        var networkInterface =
                            NetworkInterface.GetAllNetworkInterfaces()
                                            .SingleOrDefault(ni => ni.GetIPProperties()
                                                                     .UnicastAddresses
                                                                     .Any(info => info.Address.Equals(ctx.Connection.LocalIpAddress)));

                        var muxs = Id6.Format(networkInterface is { } someNetworkInterface
                                                  ? someNetworkInterface.GetPhysicalAddress().Convert48To64() : 0,
                                              Id6.FormatOptions.FixedWidth);

                        var lnsUri = await this.lnsDiscovery.ResolveLnsAsync(stationEui, cancellationToken);

                        // Ensure resilience against duplicate specification of `router-data` and make sure that LNS host address ends with slash
                        // to make sure that URI composes as expected.
                        var lnsUriSanitized = Regex.Replace(lnsUri.AbsoluteUri, @"/router-data/?$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                        lnsUriSanitized = lnsUriSanitized.EndsWith('/') ? lnsUriSanitized : $"{lnsUriSanitized}/";

                        var url = new Uri(new Uri(lnsUriSanitized), $"{DataEndpointPath}/{stationEui}");
                        var response = Write(w => WriteResponse(w, stationEui, muxs, url));
                        await s.SendAsync(response, WebSocketMessageType.Text,
                                          true, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        var response = Write(w => WriteResponse(w, stationEui, ex.Message));
                        await s.SendAsync(response, WebSocketMessageType.Text,
                                          true, cancellationToken);
                        throw;
                    }
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Writes the response for Discovery endpoint as a JSON string.
        /// </summary>
        /// <param name="writer">The write to use for serialization.</param>
        /// <param name="router">The <see cref="StationEui"/> of the querying basic station.</param>
        /// <param name="muxs">The identity of the LNS Data endpoint (<see cref="Id6"/> formatted).</param>
        /// <param name="url">The URI of the LNS Data endpoint.</param>
        internal static void WriteResponse(Utf8JsonWriter writer, StationEui router, string muxs, Uri url)
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

        internal static void WriteResponse(Utf8JsonWriter writer, StationEui router, string error)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WriteStartObject();
            writer.WriteString("router", Id6.Format(router.AsUInt64, Id6.FormatOptions.Lowercase));
            writer.WriteString("error", error);
            writer.WriteEndObject();
        }

        internal static byte[] Write(Action<Utf8JsonWriter> writer)
        {
            using var ms = new MemoryStream();
            using var jsonWriter = new Utf8JsonWriter(ms);
            writer(jsonWriter);
            jsonWriter.Flush();
            return ms.ToArray();
        }
    }
}
