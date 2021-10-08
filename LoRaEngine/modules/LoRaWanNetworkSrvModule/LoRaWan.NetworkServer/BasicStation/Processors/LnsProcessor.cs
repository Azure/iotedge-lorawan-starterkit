// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Processors
{
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class LnsProcessor : ILnsProcessor
    {
        private readonly ILogger<LnsProcessor> logger;

        public LnsProcessor(ILogger<LnsProcessor> logger)
        {
            this.logger = logger;
        }

        public Task HandleDiscoveryAsync(string input, WebSocket socket, CancellationToken token = default)
        {
            // TO DO: Reply with proper response message, before closing the socket
            this.logger.LogInformation($"Received message: {input}");
            return Task.CompletedTask;
        }

        public Task HandleDataAsync(string input, WebSocket socket, CancellationToken token = default)
        {
            // TO DO: Reply with proper response message, before closing the socket
            this.logger.LogInformation($"Received message: {input}");
            return Task.CompletedTask;
        }
    }
}
