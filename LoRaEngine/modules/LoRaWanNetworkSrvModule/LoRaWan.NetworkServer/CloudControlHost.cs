// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

    internal class CloudControlHost : IHostedService
    {
        private readonly ILnsRemoteCallListener lnsRemoteCallListener;
        private readonly ILnsRemoteCallHandler lnsRemoteCallHandler;
        private readonly string[] subscriptionChannels;

        public CloudControlHost(ILnsRemoteCallListener lnsRemoteCallListener,
                                ILnsRemoteCallHandler lnsRemoteCallHandler,
                                NetworkServerConfiguration networkServerConfiguration)
        {
            this.lnsRemoteCallListener = lnsRemoteCallListener;
            this.lnsRemoteCallHandler = lnsRemoteCallHandler;
            this.subscriptionChannels = new string[] { networkServerConfiguration.GatewayID, Constants.CloudToDeviceClearCache };
        }


        public Task StartAsync(CancellationToken cancellationToken) =>
            Task.WhenAll(this.subscriptionChannels.Select(c => this.lnsRemoteCallListener.SubscribeAsync(c,
                                                                                                         remoteCall => this.lnsRemoteCallHandler.ExecuteAsync(remoteCall, cancellationToken),
                                                                                                         cancellationToken)));

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.WhenAll(this.subscriptionChannels.Select(c => this.lnsRemoteCallListener.UnsubscribeAsync(c, cancellationToken)));
    }
}
