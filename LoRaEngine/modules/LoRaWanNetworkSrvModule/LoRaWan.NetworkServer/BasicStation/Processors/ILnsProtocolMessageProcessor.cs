// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation.Processors
{
    using Microsoft.AspNetCore.Http;
    using System;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILnsProtocolMessageProcessor
    {
        /// <summary>
        /// This method handles the LNS Discovery requests coming from Basic Station clients.
        /// </summary>
        /// <param name="json">The json string received as WebSocket payload.</param>
        /// <param name="socket">A reference to the WebSocket to be used for replying.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A boolean stating if more requests are expected on this endpoint. If false, the underlying socket should be closed.</returns>
        public Task<bool> HandleDiscoveryAsync(string json, WebSocket socket, CancellationToken token);

        /// <summary>
        /// This method handles the data requests coming from Basic Station clients.
        /// </summary>
        /// <param name="json">The json string received as WebSocket payload.</param>
        /// <param name="socket">A reference to the WebSocket to be used for replying.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A boolean stating if more requests are expected on this endpoint. If false, the underlying socket should be closed.</returns>
        public Task<bool> HandleDataAsync(string json, WebSocket socket, CancellationToken token);

        /// <summary>
        /// This method is used for binding the 'discovery' and 'data' endpoints to corresponding get routes.
        /// </summary>
        /// <param name="httpContext">The HttpContext coming from ASP.Net Core.</param>
        /// <param name="handler">The method which should handle the incoming request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The HttpContext encapsulating all information about the specific HTTP request.</returns>
        public Task<HttpContext> ProcessIncomingRequestAsync(HttpContext httpContext,
                                                             Func<string, WebSocket, CancellationToken, Task<bool>> handler,
                                                             CancellationToken cancellationToken);
    }
}
