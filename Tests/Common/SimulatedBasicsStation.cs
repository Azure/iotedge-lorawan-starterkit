// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{

    using System;
    using System.Text.Json;
    using LoRaWan.Tests.Simulation.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices;
    using LoRaWan.NetworkServer;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using System.Net.WebSockets;
    using System.Linq;
    using System.Globalization;
    using System.Threading;
    using System.Text;
    using System.Diagnostics;

    public sealed class SimulatedBasicsStation : IDisposable
    {
        private readonly StationEui stationEUI;
        private ClientWebSocket clientWebSocket = new ClientWebSocket();
        private Uri LnsUri { get; set; }

        private readonly HashSet<Func<string, bool>> subscribers = new HashSet<Func<string, bool>>();

        internal void SubscribeOnce(Func<string, bool> value)
        {
            this.subscribers.Add(value);
        }


        public SimulatedBasicsStation(StationEui stationEUI, Uri lnsUri)
        {
            this.stationEUI = stationEUI;
            LnsUri = lnsUri;
        }

        public async Task<IDisposable> StartAsync(CancellationToken cancellationToken = default)
        {
            var routerReceivedMessage = new List<string>();
            await this.clientWebSocket.ConnectAsync(new Uri(LnsUri, "router-info"), cancellationToken);

            await SendMessageAsync(JsonSerializer.Serialize(new
            {
                router = this.stationEUI.AsUInt64
            }), cancellationToken);

            var enumerator = this.clientWebSocket.ReadTextMessages(cancellationToken);
            if (!await enumerator.MoveNextAsync())
            {
                throw new InvalidOperationException("Router info endpoint should return a response.");
            }

            await StopAsync(cancellationToken);

            // we want to ensure we received a uri message
            Debug.Assert(enumerator.Current.Contains("uri", StringComparison.OrdinalIgnoreCase));

            // CONNECT ON ROUTER-DATA ENDPOINT
            await this.clientWebSocket.ConnectAsync(new Uri(LnsUri, $"router-data/{stationEUI}"), cancellationToken);

            // Send version request
            var versionMessage = new LnsVersionRequest(this.stationEUI, "2", "1", "test", 2, "");
            await SendMessageAsync(JsonSerializer.Serialize(versionMessage), cancellationToken);

            // Listen on messages and keep connection alive
            enumerator = this.clientWebSocket.ReadTextMessages(cancellationToken);
            if (!await enumerator.MoveNextAsync())
            {
                throw new InvalidOperationException("Version request should return a response.");
            }

            // move by one and ensure that the response was the router_config message
            Debug.Assert(enumerator.Current.Contains("router_config", StringComparison.OrdinalIgnoreCase));

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _ = Task.Run(async () =>
            {
                while (await enumerator.MoveNextAsync())
                {
                    var msg = enumerator.Current;
                    routerReceivedMessage.Add(msg);
                    if (this.subscribers.Count > 0)
                    {
                        var subscriberToRemove = this.subscribers.FirstOrDefault(predicate => predicate(msg));
                        if (subscriberToRemove != null)
                        {
                            this.subscribers.Remove(subscriberToRemove);
                        }
                    }

                    Console.WriteLine("Message received: " + msg);
                }
            }, cts.Token);

            return cts;
        }

        private sealed class CancelOperation : IDisposable
        {
            private readonly CancellationTokenSource cts;

            public CancelOperation(CancellationTokenSource cts) => this.cts = cts;

            public void Dispose() => this.cts.Cancel();
        }

        //// Sends unconfirmed message
        public async Task SendDataMessageAsync(LoRaRequest loRaRequest, CancellationToken cancellationToken = default)
        {
            var payload = (LoRaPayloadData)loRaRequest.Payload;

            var msg = JsonSerializer.Serialize(new
            {
                MHdr = uint.Parse(loRaRequest.Payload.MHdr.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                msgtype = "updf",
                DevAddr = int.Parse(payload.DevAddr.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                FCtrl = (uint)payload.FrameControlFlags,
                FCnt = MemoryMarshal.Read<ushort>(payload.Fcnt.Span),
                FOpts = ConversionHelper.ByteArrayToString(payload.Fopts),
                FPort = (int)payload.Fport,
                FRMPayload = ConversionHelper.ByteArrayToString(payload.Frmpayload),
                MIC = payload.Mic.Value.AsInt32,
                DR = loRaRequest.RadioMetadata.DataRate,
                Freq = loRaRequest.RadioMetadata.Frequency.AsUInt64,
                upinfo = new
                {
                    gpstime = loRaRequest.RadioMetadata.UpInfo.GpsTime,
                    rctx = 10,
                    rssi = loRaRequest.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication,
                    xtime = loRaRequest.RadioMetadata.UpInfo.Xtime,
                    snr = loRaRequest.RadioMetadata.UpInfo.SignalNoiseRatio
                }
            });

            await SendMessageAsync(msg, cancellationToken);

            TestLogger.Log($"[{payload.DevAddr}] Sending data: {payload.Frmpayload}");
        }

        internal async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await this.clientWebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await this.clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing WS", cancellationToken);
            }
            finally
            {
                this.clientWebSocket.Dispose();
            }

            this.clientWebSocket = new ClientWebSocket();
        }

        public void Dispose()
        {
            this.clientWebSocket?.Dispose();
        }
    }
}
