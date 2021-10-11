// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.Processors
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;

    public interface ILnsProtocolMessageProcessor
    {
        /// <summary>
        /// This method handles the LNS Discovery requests coming from Basic Station clients.
        /// </summary>
        /// <param name="httpContext">The HttpContext coming from ASP.Net Core.</param>
        /// <param name="token">The cancellation token.</param>
        public Task HandleDiscoveryAsync(HttpContext httpContext, CancellationToken token);

        /// <summary>
        /// This method handles the data requests coming from Basic Station clients.
        /// </summary>
        /// <param name="httpContext">The HttpContext coming from ASP.Net Core.</param>
        /// <param name="token">The cancellation token.</param>
        public Task HandleDataAsync(HttpContext httpContext, CancellationToken token);
    }
}
