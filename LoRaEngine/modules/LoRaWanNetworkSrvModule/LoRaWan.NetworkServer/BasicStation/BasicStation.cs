// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicStation.WebSocketServer;
    using LoRaWan.NetworkServer.Common;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;

    class BasicStation : PhysicalClient
    {
        private IWebHost webHost;
        private bool disposedValue;

        public BasicStation(
            NetworkServerConfiguration configuration,
            MessageDispatcher messageDispatcher,
            LoRaDeviceAPIServiceBase loRaDeviceAPIService,
            ILoRaDeviceRegistry loRaDeviceRegistry)
            : base(
                configuration,
                messageDispatcher,
                loRaDeviceAPIService,
                loRaDeviceRegistry)
        {
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.webHost.Dispose();
                }

                this.disposedValue = true;
            }
        }

        public override void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override Task RunServerProcess(CancellationToken cancellationToken)
        {
            this.webHost = WebHost.CreateDefaultBuilder()
                                  .UseUrls("http://0.0.0.0:5000")
                                  .UseStartup<LnsStartup>()
                                  .ConfigureServices(this.PopulateServiceCollection)
                                  .Build();
            return this.webHost.RunAsync(cancellationToken);
        }

        private void PopulateServiceCollection(IServiceCollection serviceCollection)
            => serviceCollection.AddSingleton(this.configuration)
                                .AddSingleton(this.messageDispatcher)
                                .AddSingleton(this.loRaDeviceAPIService)
                                .AddSingleton(this.loRaDeviceRegistry);
    }
}
