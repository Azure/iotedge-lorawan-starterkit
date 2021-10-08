// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Processors
{
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILnsProcessor
    {
        /// <summary>
        /// This method handles the LNS Discovery requests coming from Basic Station clients.
        /// </summary>
        /// <param name="input">The string received as WebSocket payload.</param>
        /// <param name="socket">A reference to the WebSocket to be used for replying.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task HandleDiscoveryAsync(string input, WebSocket socket, CancellationToken token = default);

        /// <summary>
        /// This method handles the data requests coming from Basic Station clients.
        /// </summary>
        /// <param name="input">The string received as WebSocket payload.</param>
        /// <param name="socket">A reference to the WebSocket to be used for replying.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns></returns>
        public Task HandleDataAsync(string input, WebSocket socket, CancellationToken token = default);
    }
}
