// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;

    internal class DownstreamSender : IPacketForwarder
    {
        private static readonly Action<ILogger, StationEui, int, string, Exception> LogSendingMessage =
            LoggerMessage.Define<StationEui, int, string>(LogLevel.Debug, default,
                                                     "Sending message to station with EUI '{StationEui}' with ID {Diid}. Payload '{Payload}'.");

        private readonly WebSocketWriterRegistry<StationEui, string> socketWriterRegistry;
        private readonly IBasicsStationConfigurationService basicsStationConfigurationService;
        private readonly ILogger<DownstreamSender> logger;
        private readonly Random random = new Random();

        public DownstreamSender(WebSocketWriterRegistry<StationEui, string> socketWriterRegistry,
                                IBasicsStationConfigurationService basicsStationConfigurationService,
                                ILogger<DownstreamSender> logger)
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
                this.logger.LogWarning("Could not retrieve an active connection for Station with EUI '{StationEui}'. The payload '{Payload}' will be dropped.", message.StationEui, ConversionHelper.ByteArrayToString(message.Data));
            }
        }

        private string Message(DownlinkMessage message)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            writer.WriteStartObject();

            writer.WriteString("msgtype", LnsMessageType.DownlinkMessage.ToBasicStationString());
            writer.WriteString("DevEui", message.DevEui);

            // 0 is for Class A devices, 2 is for Class C devices
            // Ideally there Class C downlink frame which answers an uplink which have RxDelay set
            var deviceClassType = message.LnsRxDelay == 0 ? LoRaDeviceClassType.C : LoRaDeviceClassType.A;
            writer.WriteNumber("dC", (int)deviceClassType);

            // Getting and writing payload bytes
            var pduBytes = message.Data;
            var pduChars = new char[pduBytes.Length * 2];
            Hexadecimal.Write(pduBytes, pduChars);
            writer.WriteString("pdu", pduChars);

#pragma warning disable CA5394 // Do not use insecure randomness. This is fine as not used for any crypto operations.
            var diid = this.random.Next(int.MinValue, int.MaxValue);
            writer.WriteNumber("diid", diid);
#pragma warning restore CA5394 // Do not use insecure randomness
            LogSendingMessage(this.logger, message.StationEui, diid, ConversionHelper.ByteArrayToString(message.Data), null);

            if (deviceClassType is LoRaDeviceClassType.A)
            {
                writer.WriteNumber("RxDelay", message.LnsRxDelay);
                writer.WriteNumber("RX1DR", (int)message.DataRateRx1);
                writer.WriteNumber("RX1Freq", message.FrequencyRx1.AsUInt64);
                writer.WriteNumber("RX2DR", (int)message.DataRateRx2);
                writer.WriteNumber("RX2Freq", message.FrequencyRx2.AsUInt64);
                writer.WriteNumber("xtime", message.Xtime);
            }
            else if (deviceClassType is LoRaDeviceClassType.C)
            {
                writer.WriteNumber("RX2DR", (int)message.DataRateRx2);
                writer.WriteNumber("RX2Freq", message.FrequencyRx2.AsUInt64);
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
