// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

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
            var stationEui = message.StationEui;
            var region = await this.basicsStationConfigurationService.GetRegionFromBasicsStationConfiguration(stationEui, CancellationToken.None);
            var payload = Message(message, region);
            await this.socketWriterRegistry.SendAsync(stationEui, payload, CancellationToken.None);
        }

        /*
         * 

             Name |  Type  | Function
            :----:|:------:|--------------------------------------------------------------
             imme | bool   | Send packet immediately (will ignore tmst & time)
             tmst | number | Send packet on a certain timestamp value (will ignore time)
             tmms | number | Send packet at a certain GPS time (GPS synchronization required)
             freq | number | TX central frequency in MHz (unsigned float, Hz precision)
             rfch | number | Concentrator "RF chain" used for TX (unsigned integer)
             powe | number | TX output power in dBm (unsigned integer, dBm precision)
             modu | string | Modulation identifier "LORA" or "FSK"
             datr | string | LoRa datarate identifier (eg. SF12BW500)
             datr | number | FSK datarate (unsigned, in bits per second)
             codr | string | LoRa ECC coding rate identifier
             fdev | number | FSK frequency deviation (unsigned integer, in Hz) 
             ipol | bool   | Lora modulation polarization inversion
             prea | number | RF preamble size (unsigned integer)
             size | number | RF packet payload size in bytes (unsigned integer)
             data | string | Base64 encoded RF packet payload, padding optional
             ncrc | bool   | If true, disable the CRC of the physical layer (optional)
         */
        private string Message(DownlinkPktFwdMessage message, Region region)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);

            writer.WriteStartObject();

            writer.WriteString("msgtype", "dnmsg");
            writer.WriteString("DevEui", message.DevEui);
            writer.WriteNumber("dC", 0); //TODO DANIELE: DEVICECLASS 
#pragma warning disable CA5394 // Do not use insecure randomness
            writer.WriteNumber("diid", new Random().Next(int.MinValue, int.MaxValue));
#pragma warning restore CA5394 // Do not use insecure randomness
            var pduBytes = Convert.FromBase64String(message.Txpk.Data);
            var pduChars = new char[pduBytes.Length * 2];
            Hexadecimal.Write(pduBytes, pduChars);
            writer.WriteString("pdu", pduChars);

#pragma warning disable CS0618 // Type or member is obsolete
            var rx1dr = region.GetDRFromFreqAndChan(message.Txpk.Datr);
#pragma warning restore CS0618 // Type or member is obsolete

            writer.WriteNumber("RxDelay", message.RxDelay);
            writer.WriteNumber("RX1DR", rx1dr);
            writer.WriteNumber("RX1Freq", (ulong)(message.Txpk.Freq * 1e6));
            writer.WriteNumber("RX2DR", region.GetDefaultRX2ReceiveWindow().DataRate);
            writer.WriteNumber("RX2Freq", (ulong)(region.GetDefaultRX2ReceiveWindow().Frequency * 1e6));
            writer.WriteNumber("priority", 0);
            writer.WriteNumber("xtime", this.radioMetadata.Xtime);
            writer.WriteNumber("rctx", this.radioMetadata.AntennaPreference);
            writer.WriteEndObject();

            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
