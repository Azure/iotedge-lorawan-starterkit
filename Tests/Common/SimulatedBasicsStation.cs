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

    public sealed class SimulatedBasicsStation : IDisposable
    {
        private readonly string stationEUI;
        private WebsocketClient DataWebsocketClient { get; set; }
        private Uri LnsUri { get; set; }

        private readonly HashSet<Func<string, bool>> subscribers = new HashSet<Func<string, bool>>();

        internal void SubscribeOnce(Func<string, bool> value)
        {
            this.subscribers.Add(value);
        }


        public SimulatedBasicsStation(string stationEUI, Uri lnsUri)
        {
            this.stationEUI = stationEUI;
            this.LnsUri = lnsUri;
        }

        public async Task StartAsync()
        {
            try
            {
                var receivedMessage = new List<string>();
                using var routerWebsocketClient = new WebsocketClient(new Uri(LnsUri, "router-info"))
                {
                    ReconnectTimeout = TimeSpan.FromSeconds(5)
                };
                routerWebsocketClient.ReconnectionHappened.Subscribe(info =>
                {
                    Console.WriteLine("Reconnection happened, type: " + info.Type);
                });
                routerWebsocketClient.MessageReceived.Subscribe(msg =>
                {
                    receivedMessage.Add(msg.Text);
                    Console.WriteLine("Message received: " + msg);
                });
                await routerWebsocketClient.Start();
                //Task.Run(() => client.Send("{ message }"));
                using var ms = new MemoryStream();
                using var writer = new Utf8JsonWriter(ms);

                await routerWebsocketClient.SendInstant(JsonSerializer.Serialize(new
                {
                    router = this.stationEUI
                }));

                await Task.Delay(5000);
                await routerWebsocketClient.Stop(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "closing WS");
                var factory = new Func<ClientWebSocket>(() => new ClientWebSocket
                {
                    Options =
                        {
                            KeepAliveInterval = TimeSpan.FromSeconds(5),
                        }
                });

                DataWebsocketClient = new WebsocketClient(new Uri(LnsUri, $"router-data/{stationEUI}"), factory)
                {
                    ReconnectTimeout = TimeSpan.FromSeconds(5)
                };
                DataWebsocketClient.ReconnectionHappened.Subscribe(info =>
                {
                    Console.WriteLine("Reconnection happened, type: " + info.Type);
                });
                DataWebsocketClient.MessageReceived.Subscribe(msg =>
                {
                    receivedMessage.Add(msg.Text);
                    if (this.subscribers.Count > 0)
                    {
                        Func<string, bool> subscriberToRemove = null;

                        foreach (var subscriber in this.subscribers)
                        {
                            if (subscriber(msg.Text))
                            {
                                subscriberToRemove = subscriber;
                                break;
                            }
                        }

                        if (subscriberToRemove != null)
                        {
                            this.subscribers.Remove(subscriberToRemove);
                        }
                    }
                    Console.WriteLine("Message received: " + msg);
                });
                await DataWebsocketClient.Start();

                var versionMessage = new LnsVersionRequest(stationEUI , "2", "1", "test", 2, "");
                await DataWebsocketClient.SendInstant(JsonSerializer.Serialize(versionMessage));
                await Task.Delay(5000);

            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.ToString());
            }
        }

        //// Sends unconfirmed message
        public async Task SendDataMessageAsync(LoRaRequest LoRaRequest)
        {
            var payload = (LoRaPayloadData)LoRaRequest.Payload;
            payload.DevAddr.Span.Reverse();
            var msg = JsonSerializer.Serialize(new {
                MHdr = (uint)LoRaRequest.Payload.Mhdr.Span[0],
                msgtype = "updf",
                DevAddr = MemoryMarshal.Read<int>(payload.DevAddr.Span),
                FCtrl = (uint)payload.FrameControlFlags,
                FCnt = MemoryMarshal.Read<ushort>(payload.Fcnt.Span),
                FOpts = ConversionHelper.ByteArrayToString(payload.Fopts),
                FPort = (int)payload.Fport,
                FRMPayload = ConversionHelper.ByteArrayToString(payload.Frmpayload),
                MIC = MemoryMarshal.Read<int>(payload.Mic.Span),
                DR = LoRaRequest.RadioMetadata.DataRate,
                Freq = LoRaRequest.RadioMetadata.Frequency.AsUInt64,
                upinfo = new
                {
                    gpstime = LoRaRequest.RadioMetadata.UpInfo.GpsTime,
                    rctx = 10,
                    rssi = LoRaRequest.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication,
                    xtime = LoRaRequest.RadioMetadata.UpInfo.Xtime,
                    snr = LoRaRequest.RadioMetadata.UpInfo.SignalNoiseRatio
                }
            });
           
            await SendMessageAsync(msg);

            // TestLogger.Log($"[{LoRaDevice.DevAddr}] Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.UTF8.GetString(gatewayInfo)}");
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
            DataWebsocketClient.Dispose();
        }
    }
}
