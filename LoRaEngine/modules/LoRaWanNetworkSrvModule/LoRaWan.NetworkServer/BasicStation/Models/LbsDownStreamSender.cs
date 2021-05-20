// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicStation.Models;
    using LoRaWan.NetworkServer.BasicStation.WebSocketServer;
    using Newtonsoft.Json;

    public class LbsDownStreamSender
    {
        private readonly WebSocket websocket;

        public LbsDownStreamSender(WebSocket websocket)
        {
            this.websocket = websocket;
        }

        internal async Task SendDownstreamAsync(LbsClassADownlink lbsClassAPayload)
        {
            var options = new JsonSerializerOptions
            {
                IgnoreNullValues = true,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            };

            var message = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(lbsClassAPayload, options));
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(lbsClassAPayload, options));
            await this.websocket.SendAsync(new ReadOnlyMemory<byte>(message, 0, message.Length), System.Net.WebSockets.WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}