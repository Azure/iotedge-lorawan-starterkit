// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{

    using System;
    using System.Text.Json;
    using LoRaWan.Tests.Simulation.Models;
    using Websocket.Client;
    using System.IO;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Runtime.InteropServices;
    using LoRaWan.NetworkServer;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using System.Net.WebSockets;
    using System.Linq;
    using System.Globalization;

    public sealed class SimulatedBasicsStation : IDisposable
    {
        private readonly StationEui stationEUI;
        private WebsocketClient DataWebsocketClient { get; set; }
        private Uri LnsUri { get; set; }

        private readonly HashSet<Func<string, bool>> subscribers = new HashSet<Func<string, bool>>();

        internal void SubscribeOnce(Func<string, bool> value)
        {
            this.subscribers.Add(value);
        }


        public SimulatedBasicsStation(StationEui stationEUI, Uri lnsUri)
        {
            this.stationEUI = stationEUI;
            this.LnsUri = lnsUri;
        }

        public async Task StartAsync()
        {
            var routerReceivedMessage = new List<string>();
            using var routerWebsocketClient = new WebsocketClient(new Uri(LnsUri, "router-info"));
            routerWebsocketClient.ReconnectionHappened.Subscribe(info =>
            {
                Console.WriteLine("Reconnection happened, type: " + info.Type);
            });
            routerWebsocketClient.MessageReceived.Subscribe(msg =>
            {
                routerReceivedMessage.Add(msg.Text);
                Console.WriteLine("Message received: " + msg);
            });
            await routerWebsocketClient.Start();
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            var msg = JsonSerializer.Serialize(new
            {
                router = this.stationEUI.AsUInt64
            });
            await routerWebsocketClient.SendInstant(JsonSerializer.Serialize(new
            {
                router = this.stationEUI.AsUInt64
            }));

            // we want to ensure we received a uri message
            await AssertUtils.ContainsWithRetriesAsync(x => x.Contains("uri", StringComparison.OrdinalIgnoreCase), routerReceivedMessage, interval: TimeSpan.FromSeconds(5d));

            await routerWebsocketClient.Stop(WebSocketCloseStatus.NormalClosure, "closing WS");
            var factory = new Func<ClientWebSocket>(() => new ClientWebSocket
            {
                Options =
                        {
                            KeepAliveInterval = TimeSpan.FromSeconds(5),
                        }
            });

            DataWebsocketClient = new WebsocketClient(new Uri(LnsUri, $"router-data/{ stationEUI }"), factory);
            DataWebsocketClient.ReconnectionHappened.Subscribe(info =>
            {
                Console.WriteLine("Reconnection happened, type: " + info.Type);
            });
            DataWebsocketClient.MessageReceived.Subscribe(msg =>
            {
                routerReceivedMessage.Add(msg.Text);
                if (this.subscribers.Count > 0)
                {
                    Func<string, bool> subscriberToRemove = null;

                    foreach (var subscriber in this.subscribers.Where(subscriber => subscriber(msg.Text)))
                    {
                        subscriberToRemove = subscriber;
                        break;
                    }

                    if (subscriberToRemove != null)
                    {
                        this.subscribers.Remove(subscriberToRemove);
                    }
                }
                Console.WriteLine("Message received: " + msg);
            });
            await DataWebsocketClient.Start();

            var versionMessage = new LnsVersionRequest(stationEUI, "2", "1", "test", 2, "");
            await DataWebsocketClient.SendInstant(JsonSerializer.Serialize(versionMessage));
            // we want to ensure we received a router config message
            await AssertUtils.ContainsWithRetriesAsync(x => x.Contains("router_config", StringComparison.OrdinalIgnoreCase), routerReceivedMessage, interval: TimeSpan.FromSeconds(5d));

            await Task.Delay(5000);
        }

        //// Sends unconfirmed message
        public async Task SendDataMessageAsync(LoRaRequest loRaRequest)
        {
            var payload = (LoRaPayloadData)loRaRequest.Payload;

            var fopts = payload.MacCommands.SelectMany(mc => mc.ToBytes()).ToArray();
            var foptsHex = new char[fopts.Length * 2];
            Hexadecimal.Write(fopts, foptsHex);

            var msg = JsonSerializer.Serialize(new
            {
                MHdr = uint.Parse(loRaRequest.Payload.MHdr.ToString(), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                msgtype = "updf",
                DevAddr = int.Parse(payload.DevAddr.ToString(), System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                FCtrl = (uint)payload.FrameControlFlags,
                FCnt = MemoryMarshal.Read<ushort>(payload.Fcnt.Span),
                FOpts = foptsHex,
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

            await SendMessageAsync(msg);

            TestLogger.Log($"[{payload.DevAddr}] Sending data: {payload.Frmpayload}");
        }

        internal async Task SendMessageAsync(string vs)
        {
            await DataWebsocketClient.SendInstant(vs);
        }

        public async Task StopAsync()
        {
            await DataWebsocketClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "closing WS");
        }

        public void Dispose()
        {
            DataWebsocketClient?.Dispose();
        }
    }
}
