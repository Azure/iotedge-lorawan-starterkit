// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface INetworkServer : IDisposable
    {
        /// <summary>
        /// The following method is starting the NetworkServer implementation.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        public Task RunServerAsync(CancellationToken cancellationToken = default);
    }
}
