// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{

    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaTools.LoRaMessage;
    using System.Text.Json.Serialization;
    using LoRaTools;

    public sealed class SimulatedBasicsStation : IDisposable
    {
        private readonly StationEui stationEUI;
        private ClientWebSocket clientWebSocket = CreateClientWebSocket();
        private readonly Uri lnsUri;
        private CancellationTokenSource cancellationTokenSource;
        private bool started;
        private Task processMessagesAsync;

        public event EventHandler<EventArgs<string>> MessageReceived;

        public SimulatedBasicsStation(StationEui stationEUI, Uri lnsUri)
        {
            this.stationEUI = stationEUI;
            this.lnsUri = lnsUri;
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (this.started) return;
            this.started = true;

            // Handle router-info calls
            await this.clientWebSocket.ConnectAsync(new Uri(this.lnsUri, "router-info"), cancellationToken);
            await SerializeAndSendMessageAsync(new { router = this.stationEUI.AsUInt64 }, cancellationToken);
            var enumerator = this.clientWebSocket.ReadTextMessages(cancellationToken);
            if (!await enumerator.MoveNextAsync())
                throw new InvalidOperationException("Router info endpoint should return a response.");
            await StopAsync(cancellationToken);
            Debug.Assert(enumerator.Current.Contains("uri", StringComparison.OrdinalIgnoreCase));

            // Handle router-data calls
            await this.clientWebSocket.ConnectAsync(new Uri(this.lnsUri, $"router-data/{this.stationEUI}"), cancellationToken);

            // Send version request
            await SerializeAndSendMessageAsync(new LnsVersionRequest(this.stationEUI.ToString(), "2", "1", "test", 2, ""), cancellationToken);
            this.cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            enumerator = this.clientWebSocket.ReadTextMessages(this.cancellationTokenSource.Token);
            if (!await enumerator.MoveNextAsync())
                throw new InvalidOperationException("Version request should return a response.");
            Debug.Assert(enumerator.Current.Contains("router_config", StringComparison.OrdinalIgnoreCase));

            // Listen to incoming messages
            this.processMessagesAsync = Task.Run(async () =>
            {
                while (await enumerator.MoveNextAsync())
                {
                    var msg = enumerator.Current;
                    MessageReceived?.Invoke(this, new EventArgs<string>(msg));
                    Console.WriteLine("Message received: " + msg);
                }

                Console.WriteLine($"Stopping message processing in station {this.stationEUI}");
            }, this.cancellationTokenSource.Token);
        }

        //// Sends unconfirmed message
        public async Task SendDataMessageAsync(LoRaRequest loRaRequest, CancellationToken cancellationToken = default)
        {
            var payload = (LoRaPayloadData)loRaRequest.Payload;

            await SerializeAndSendMessageAsync(
                new UpstreamDataRequest(MHdr: uint.Parse(loRaRequest.Payload.MHdr.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                                        DevAddr: int.Parse(payload.DevAddr.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                                        FCtrl: (uint)payload.FrameControlFlags,
                                        FCnt: payload.Fcnt,
                                        FOpts: payload.Fopts.ToHex(),
                                        FPort: (int)payload.Fport,
                                        FrmPayload: payload.Frmpayload.ToHex(),
                                        Mic: payload.Mic.Value.AsInt32,
                                        Dr: loRaRequest.RadioMetadata.DataRate,
                                        Freq: loRaRequest.RadioMetadata.Frequency.AsUInt64,
                                        UpInfo: new UpInfo(GpsTime: loRaRequest.RadioMetadata.UpInfo.GpsTime,
                                                           Rctx: 10,
                                                           Rssi: loRaRequest.RadioMetadata.UpInfo.ReceivedSignalStrengthIndication,
                                                           Xtime: loRaRequest.RadioMetadata.UpInfo.Xtime,
                                                           Snr: loRaRequest.RadioMetadata.UpInfo.SignalNoiseRatio)), cancellationToken);
        }

        /// <summary>
        /// Serializes and sends a message.
        /// </summary>
        internal async Task SerializeAndSendMessageAsync(object obj, CancellationToken cancellationToken = default)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(obj));
            await this.clientWebSocket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
        }

        private async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!this.started)
                throw new InvalidOperationException("Start the simulated Basics Station first.");

            try
            {
                await this.clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing WS", cancellationToken);
                this.cancellationTokenSource?.Cancel();
            }
            finally
            {
                this.clientWebSocket.Dispose();
            }

            // The ClientWebSocket needs to be disposed and recreated in order to be used again
            this.clientWebSocket = CreateClientWebSocket();
        }

        public async Task StopAndValidateAsync(CancellationToken cancellationToken = default)
        {
            await StopAsync(cancellationToken);

            try
            {
                await this.processMessagesAsync;
            }
            catch (OperationCanceledException)
            {
                // Expected as websocket reading is canceled through Cancellation Token.
            }
        }

        private static ClientWebSocket CreateClientWebSocket()
        {
            var result = new ClientWebSocket();
#pragma warning disable CA5359 // Do Not Disable Certificate Validation (using self-signed certificates on the LNS, instead of trusting the certificate we disable the validation).
            result.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359 // Do Not Disable Certificate Validation
            return result;
        }

        public void Dispose()
        {
            this.cancellationTokenSource?.Cancel();
            this.cancellationTokenSource?.Dispose();
            this.clientWebSocket?.Dispose();
        }

        private sealed record LnsVersionRequest([property: JsonPropertyName("station")] string Station,
                                                [property: JsonPropertyName("firmware")] string Firmware,
                                                [property: JsonPropertyName("package")] string Package,
                                                [property: JsonPropertyName("model")] string Model,
                                                [property: JsonPropertyName("protocol")] int Protocol,
                                                [property: JsonPropertyName("features")] string Features)
        {
            [JsonPropertyName("msgtype")]
            public string MessageType { get; } = "version";
        }

        private sealed record UpstreamDataRequest([property: JsonPropertyName("MHdr")] uint MHdr,
                                                  [property: JsonPropertyName("DevAddr")] int DevAddr,
                                                  [property: JsonPropertyName("FCtrl")] uint FCtrl,
                                                  [property: JsonPropertyName("FCnt")] ushort FCnt,
                                                  [property: JsonPropertyName("FOpts")] string FOpts,
                                                  [property: JsonPropertyName("FPort")] int FPort,
                                                  [property: JsonPropertyName("FRMPayload")] string FrmPayload,
                                                  [property: JsonPropertyName("MIC")] int Mic,
                                                  [property: JsonPropertyName("DR")] DataRateIndex Dr,
                                                  [property: JsonPropertyName("Freq")] ulong Freq,
                                                  [property: JsonPropertyName("upinfo")] UpInfo UpInfo)
        {
            [JsonPropertyName("msgtype")]
            public string MsgType { get; } = "updf";
        };

        private sealed record UpInfo([property: JsonPropertyName("gpstime")] uint GpsTime,
                                     [property: JsonPropertyName("rctx")] int Rctx,
                                     [property: JsonPropertyName("rssi")] double Rssi,
                                     [property: JsonPropertyName("xtime")] ulong Xtime,
                                     [property: JsonPropertyName("snr")] float Snr);
    }
}
