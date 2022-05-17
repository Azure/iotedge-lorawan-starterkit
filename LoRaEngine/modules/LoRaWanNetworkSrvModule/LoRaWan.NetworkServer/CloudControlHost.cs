// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    internal class CloudControlHost : IHostedService
    {
        private readonly ILnsRemoteCallListener lnsRemoteCallListener;
        private readonly ILnsRemoteCallHandler lnsRemoteCallHandler;
        private readonly string gatewayId;

        public CloudControlHost(ILnsRemoteCallListener lnsRemoteCallListener,
                                ILnsRemoteCallHandler lnsRemoteCallHandler,
                                NetworkServerConfiguration networkServerConfiguration)
        {
            this.lnsRemoteCallListener = lnsRemoteCallListener;
            this.lnsRemoteCallHandler = lnsRemoteCallHandler;
            this.gatewayId = networkServerConfiguration.GatewayID;
        }

        public Task StartAsync(CancellationToken cancellationToken) =>
            this.lnsRemoteCallListener.SubscribeAsync(this.gatewayId,
                                                      remoteCall => this.lnsRemoteCallHandler.ExecuteAsync(remoteCall, cancellationToken),
                                                      cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) =>
            this.lnsRemoteCallListener.UnsubscribeAsync(this.gatewayId, cancellationToken);
    }
}
