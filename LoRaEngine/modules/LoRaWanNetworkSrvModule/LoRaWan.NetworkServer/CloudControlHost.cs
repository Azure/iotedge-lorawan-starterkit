// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    internal class CloudControlHost : IHostedService
    {
        private readonly ILnsRemoteCallListener lnsRemoteCallListener;
        private readonly ILnsRemoteCallHandler lnsRemoteCallHandler;
        private readonly NetworkServerConfiguration networkServerConfiguration;

        public CloudControlHost(ILnsRemoteCallListener lnsRemoteCallListener,
                                ILnsRemoteCallHandler lnsRemoteCallHandler,
                                NetworkServerConfiguration networkServerConfiguration)
        {
            this.lnsRemoteCallListener = lnsRemoteCallListener;
            this.lnsRemoteCallHandler = lnsRemoteCallHandler;
            this.networkServerConfiguration = networkServerConfiguration;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await this.lnsRemoteCallListener.SubscribeAsync(this.networkServerConfiguration.GatewayID,
                                                            async (remotecall) => await this.lnsRemoteCallHandler.ExecuteAsync(remotecall, cancellationToken),
                                                            cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await this.lnsRemoteCallListener.UnsubscribeAsync(this.networkServerConfiguration.GatewayID, cancellationToken);
        }
    }
}
