// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
    using Microsoft.ApplicationInsights;
    using Microsoft.AspNetCore;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Extensions.DependencyInjection;

    public static class BasicsStationNetworkServer
    {
        internal const string DiscoveryEndpoint = "/router-info";
        internal const string RouterIdPathParameterName = "routerId";
        internal const string DataEndpoint = "/router-data";
        internal const string UpdateInfoEndpoint = "/update-info";
        internal const int LnsSecurePort = 5001;
        internal const int LnsPort = 5000;
        internal const int CupsPort = 443;

        public static async Task RunServerAsync(NetworkServerConfiguration configuration, CancellationToken cancellationToken)
        {
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            var shouldUseCertificate = !string.IsNullOrEmpty(configuration.LnsServerPfxPath);
            using var webHost = WebHost.CreateDefaultBuilder()
                                       .UseUrls(shouldUseCertificate ? new[] { FormattableString.Invariant($"https://0.0.0.0:{LnsSecurePort}"),
                                                                               FormattableString.Invariant($"https://0.0.0.0:{CupsPort}") }
                                                                     : new[] { FormattableString.Invariant($"http://0.0.0.0:{LnsPort}") })
                                       .UseStartup<BasicsStationNetworkServerStartup>()
                                       .UseKestrel(config =>
                                       {
                                           if (shouldUseCertificate)
                                           {
                                               config.ConfigureHttpsDefaults(https => ConfigureHttpsSettings(configuration,
                                                                                                             config.ApplicationServices.GetService<IClientCertificateValidatorService>(),
                                                                                                             https));
                                           }
                                       })
                                       .Build();

            var telemetryClient = webHost.Services.GetService<TelemetryClient>();

            try
            {
                // We want to make sure the module connection is started at the start of the Network server.
                // This is needed when we run as module, therefore we are blocking.
                if (configuration.RunningAsIoTEdgeModule)
                {
                    var moduleConnection = webHost.Services.GetRequiredService<ModuleConnectionHost>();
                    await moduleConnection.CreateAsync(cancellationToken);
                }

                await webHost.RunAsync(cancellationToken);
            }
            finally
            {
                telemetryClient?.Flush();
                await Task.Delay(TimeSpan.FromSeconds(5), CancellationToken.None);
            }
        }

        internal static void ConfigureHttpsSettings(NetworkServerConfiguration configuration,
                                                    IClientCertificateValidatorService? clientCertificateValidatorService,
                                                    HttpsConnectionAdapterOptions https)
        {
            https.ServerCertificate = string.IsNullOrEmpty(configuration.LnsServerPfxPassword) ? new X509Certificate2(configuration.LnsServerPfxPath)
                                                                                               : new X509Certificate2(configuration.LnsServerPfxPath,
                                                                                                                      configuration.LnsServerPfxPassword,
                                                                                                                      X509KeyStorageFlags.DefaultKeySet);

            if (configuration.ClientCertificateMode is not ClientCertificateMode.NoCertificate)
            {
                if (clientCertificateValidatorService is null)
                    throw new ArgumentNullException(nameof(clientCertificateValidatorService));
                https.ClientCertificateMode = configuration.ClientCertificateMode;
                https.ClientCertificateValidation = (cert, chain, err) => clientCertificateValidatorService.ValidateAsync(cert, chain, err, default).GetAwaiter().GetResult();
            }
        }
    }
}
