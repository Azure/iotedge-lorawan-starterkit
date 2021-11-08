// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.DependencyInjection;

    public static class BasicsStationNetworkServer
    {
        internal const string DiscoveryEndpoint = "/router-info";
        internal const string RouterIdPathParameterName = "routerId";
        internal const string DataEndpoint = "/router-data";
        private const int SecurePort = 5001;
        private const int Port = 5000;

        public static async Task RunServerAsync(NetworkServerConfiguration configuration, CancellationToken cancellationToken)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            var endpoints = new List<string>()
            {
                FormattableString.Invariant($"http://0.0.0.0:{Port}")
            };

            if (!string.IsNullOrEmpty(configuration.LnsServerPfxPath))
            {
                endpoints.Add(FormattableString.Invariant($"https://0.0.0.0:{SecurePort}"));
            }

            using var webHost = WebHost.CreateDefaultBuilder()
                                       .UseUrls(endpoints.ToArray())
                                       .UseStartup<BasicsStationNetworkServerStartup>()
                                       .UseKestrel(config =>
                                       {
                                           if (!string.IsNullOrEmpty(configuration.LnsServerPfxPath))
                                           {
                                               config.ConfigureHttpsDefaults(https =>
                                               {
                                                   https.ServerCertificate = string.IsNullOrEmpty(configuration.LnsServerPfxPassword)
                                                                             ? new X509Certificate2(configuration.LnsServerPfxPath)
                                                                             : new X509Certificate2(configuration.LnsServerPfxPath,
                                                                                                    configuration.LnsServerPfxPassword,
                                                                                                    X509KeyStorageFlags.DefaultKeySet);
                                               });
                                           }
                                       })
                                       .Build();

            // We want to make sure the module connection is started at the start of the Network server.
            // This is needed when we run as module, therefore we are blocking.
            if (configuration.RunningAsIoTEdgeModule)
            {
                var moduleConnection = webHost.Services.GetRequiredService<ModuleConnectionHost>();
                await moduleConnection.CreateAsync(cancellationToken);
            }

            await webHost.RunAsync(cancellationToken);
        }
    }
}
