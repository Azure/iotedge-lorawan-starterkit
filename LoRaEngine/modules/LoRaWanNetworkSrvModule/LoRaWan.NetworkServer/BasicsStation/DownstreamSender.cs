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
    using LoRaTools.Regions;

    internal class DownstreamSender : IPacketForwarder
    {
        private readonly WebSocketWriterRegistry<StationEui, string> socketWriterRegistry;
        private readonly IBasicsStationConfigurationService basicsStationConfigurationService;

        public DownstreamSender(WebSocketWriterRegistry<StationEui, string> socketWriterRegistry, IBasicsStationConfigurationService basicsStationConfigurationService)
        {
            this.socketWriterRegistry = socketWriterRegistry;
            this.basicsStationConfigurationService = basicsStationConfigurationService;
        }

        public async Task SendDownstreamAsync(DownlinkPktFwdMessage message)
        {
            if (message is null) throw new ArgumentNullException(nameof(message));
            if (message.StationEui == default) throw new ArgumentException($"A proper StationEui needs to be set. Received '{message.StationEui}'.");

            if (this.socketWriterRegistry.TryGetHandle(message.StationEui, out var webSocketWriterHandle))
            {
                var region = await this.basicsStationConfigurationService.GetRegionAsync(message.StationEui, CancellationToken.None);
                var payload = Message(message, region);
                await webSocketWriterHandle.SendAsync(payload, CancellationToken.None);
            };
        }

        private static string Message(DownlinkPktFwdMessage message, Region region)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            writer.WriteStartObject();

            writer.WriteString("msgtype", "dnmsg");
            writer.WriteString("DevEui", message.DevEui);

            // 0 is for Class A devices, 2 is for Class C devices
            // Ideally there Class C downlink frame which answers an uplink which have Tmst and RxDelay set
            var deviceClassType = message.Txpk.Tmst == 0 && message.LnsRxDelay == 0 ? LoRaDeviceClassType.C : LoRaDeviceClassType.A;
            writer.WriteNumber("dC", (int)deviceClassType);

#pragma warning disable CA5394 // Do not use insecure randomness
            writer.WriteNumber("diid", new Random().Next(int.MinValue, int.MaxValue));
#pragma warning restore CA5394 // Do not use insecure randomness
            var pduBytes = Convert.FromBase64String(message.Txpk.Data);
            var pduChars = new char[pduBytes.Length * 2];
            Hexadecimal.Write(pduBytes, pduChars);
            writer.WriteString("pdu", pduChars);

#pragma warning disable CS0618 // Type or member is obsolete
            var dataRate = region.GetDRFromFreqAndChan(message.Txpk.Datr);
#pragma warning restore CS0618 // Type or member is obsolete

            if (deviceClassType is LoRaDeviceClassType.A)
            {
                writer.WriteNumber("RxDelay", message.LnsRxDelay);
                writer.WriteNumber("RX1DR", dataRate);
                writer.WriteNumber("RX1Freq", (ulong)(message.Txpk.Freq * 1e6));
                writer.WriteNumber("RX2DR", region.GetDefaultRX2ReceiveWindow().DataRate);
                writer.WriteNumber("RX2Freq", (ulong)(region.GetDefaultRX2ReceiveWindow().Frequency * 1e6));
                writer.WriteNumber("xtime", message.Xtime);
            }
            else if (deviceClassType is LoRaDeviceClassType.C)
            {
                writer.WriteNumber("RX2DR", dataRate);
                writer.WriteNumber("RX2Freq", (ulong)(message.Txpk.Freq * 1e6));
            }
            writer.WriteNumber("priority", 0);
            if (message.AntennaPreference.HasValue)
            {
                writer.WriteNumber("rctx", message.AntennaPreference.Value);
            }
            writer.WriteEndObject();

            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
