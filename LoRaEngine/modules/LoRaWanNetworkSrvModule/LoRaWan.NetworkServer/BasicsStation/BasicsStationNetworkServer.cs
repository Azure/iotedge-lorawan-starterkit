// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;

    public static class BasicsStationNetworkServer
    {
        internal const string DiscoveryEndpoint = "/router-info";
        internal const string RouterIdPathParameterName = "routerId";
        internal const string DataEndpoint = "/router-data";
        private const int SecurePort = 5001;
        private const int Port = 5000;

        public static async Task RunServerAsync(CancellationToken cancellationToken)
        {
            using var webHost = WebHost.CreateDefaultBuilder()
                                       .UseUrls(FormattableString.Invariant($"http://0.0.0.0:{Port}"),
                                                FormattableString.Invariant($"https://0.0.0.0:{SecurePort}"))
                                       .UseStartup<BasicsStationNetworkServerStartup>()
                                       .UseKestrel()
                                       .Build();
            await webHost.RunAsync(cancellationToken);
        }
    }
}
