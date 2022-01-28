// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;
    using Microsoft.Extensions.Logging;

    internal class DownstreamMessageSender : IDownstreamMessageSender
    {
        private static readonly Action<ILogger, StationEui, int, string, Exception> LogSendingMessage =
            LoggerMessage.Define<StationEui, int, string>(LogLevel.Debug, default,
                                                     "sending message to station with EUI '{StationEui}' with diid {Diid}. Payload '{Payload}'.");

        private readonly WebSocketWriterRegistry<StationEui, string> socketWriterRegistry;
        private readonly IBasicsStationConfigurationService basicsStationConfigurationService;
        private readonly ILogger<DownstreamMessageSender> logger;
        private readonly Random random = new Random();

        public DownstreamMessageSender(WebSocketWriterRegistry<StationEui, string> socketWriterRegistry,
                                       IBasicsStationConfigurationService basicsStationConfigurationService,
                                       ILogger<DownstreamMessageSender> logger)
        {
            this.socketWriterRegistry = socketWriterRegistry;
            this.basicsStationConfigurationService = basicsStationConfigurationService;
            this.logger = logger;
        }

        public async Task SendDownstreamAsync(DownlinkMessage message)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            if (message.StationEui == default) throw new ArgumentException($"A proper StationEui needs to be set. Received '{message.StationEui}'.");

            if (this.socketWriterRegistry.TryGetHandle(message.StationEui, out var webSocketWriterHandle))
            {
                var payload = Message(message);
                await webSocketWriterHandle.SendAsync(payload, CancellationToken.None);
            }
            else
            {
                this.logger.LogWarning("Could not retrieve an active connection for Station with EUI '{StationEui}'. The payload '{Payload}' will be dropped.", message.StationEui, message.Data.ToHex());
            }
        }

        private string Message(DownlinkMessage message)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            writer.WriteStartObject();

            writer.WriteString("msgtype", LnsMessageType.DownlinkMessage.ToBasicStationString());
            writer.WriteString("DevEui", message.DevEui.ToString());

            writer.WriteNumber("dC", message.DeviceClassType switch
            {
                LoRaDeviceClassType.A => 0,
                LoRaDeviceClassType.B => 1,
                LoRaDeviceClassType.C => 2,
                _ => throw new SwitchExpressionException(),
            });

            // Getting and writing payload bytes
            var pduBytes = message.Data;
            var pduChars = new char[pduBytes.Length * 2];
            Hexadecimal.Write(pduBytes.Span, pduChars);
            writer.WriteString("pdu", pduChars);

#pragma warning disable CA5394 // Do not use insecure randomness. This is fine as not used for any crypto operations.
            var diid = this.random.Next(int.MinValue, int.MaxValue);
            writer.WriteNumber("diid", diid);
#pragma warning restore CA5394 // Do not use insecure randomness
            LogSendingMessage(this.logger, message.StationEui, diid, message.Data.ToHex(), null);

            switch (message.DeviceClassType)
            {
                case LoRaDeviceClassType.A:
                    writer.WriteNumber("RxDelay", message.LnsRxDelay.ToSeconds());
                    if (message.Rx1 is var (datr, freq))
                    {
                        writer.WriteNumber("RX1DR", (int)datr);
                        writer.WriteNumber("RX1Freq", (ulong)freq);
                    }
                    writer.WriteNumber("RX2DR", (int)message.Rx2.DataRate);
                    writer.WriteNumber("RX2Freq", (ulong)message.Rx2.Frequency);
                    writer.WriteNumber("xtime", message.Xtime);
                    break;
                case LoRaDeviceClassType.B:
                    throw new NotSupportedException($"{nameof(DownstreamMessageSender)} does not support class B devices yet.");
                case LoRaDeviceClassType.C:
                    // if Xtime is not zero, it means that we are answering to a previous message
                    if (message.Xtime != 0)
                    {
                        writer.WriteNumber("RxDelay", message.LnsRxDelay.ToSeconds());
                        writer.WriteNumber("xtime", message.Xtime);
                        if (message.Rx1 is var (datrC, freqC))
                        {
                            writer.WriteNumber("RX1DR", (int)datrC);
                            writer.WriteNumber("RX1Freq", (ulong)freqC);
                        }
                    }
                    writer.WriteNumber("RX2DR", (int)message.Rx2.DataRate);
                    writer.WriteNumber("RX2Freq", (ulong)message.Rx2.Frequency);
                    break;
                default:
                    throw new SwitchExpressionException();
            }

            if (message.AntennaPreference.HasValue)
            {
                writer.WriteNumber("rctx", message.AntennaPreference.Value);
            }

            writer.WriteNumber("priority", 0); // Currently always setting to maximum priority.

            writer.WriteEndObject();

            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
