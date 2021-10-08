// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;

    public class BasicStationServer : INetworkServer
    {
        const int TLS_PORT = 5001;
        const int NON_TLS_PORT = 5000;
        private IWebHost webHost;
        private bool disposedValue;

        public static BasicStationServer Create() => new();

        public Task RunServerAsync(CancellationToken cancellationToken)
        {
            this.webHost = WebHost.CreateDefaultBuilder()
                                  .UseUrls($"http://0.0.0.0:{NON_TLS_PORT}", $"https://0.0.0.0:{TLS_PORT}")
                                  .UseStartup<BasicStationStartup>()
                                  .UseKestrel()
                                  .Build();
            return this.webHost.RunAsync(cancellationToken);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    webHost.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}