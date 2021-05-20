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

    public class LbsDownStreamSender
    {
        private readonly WebSocket socket;

        public LbsDownStreamSender(WebSocket socket)
        {
            this.socket = socket;
        }

        internal async Task SendDownstreamAsync(LbsClassADownlink lbsClassAPayload)
        {
            var options = new JsonSerializerOptions
            {
                IgnoreNullValues = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            var message = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(lbsClassAPayload, options));

            await this.socket.SendAsync(new ReadOnlyMemory<byte>(message, 0, message.Length), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}