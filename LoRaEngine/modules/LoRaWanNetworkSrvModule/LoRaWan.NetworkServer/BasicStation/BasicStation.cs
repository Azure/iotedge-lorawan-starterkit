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

    class BasicStation : IPhysicalClient
    {
        private IWebHost webHost;
        private bool disposedValue;

        public Task RunServer(CancellationToken cancellationToken)
        {
            this.webHost = WebHost.CreateDefaultBuilder()
                                  .UseUrls("http://0.0.0.0:5000")
                                  .UseStartup<LnsStartup>()
                                  .Build();
            return this.webHost.RunAsync(cancellationToken);
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

        public void Dispose()
        {
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
