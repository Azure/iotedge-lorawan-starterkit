// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Linq;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    internal class ClientCertificateValidator
    {
        private readonly IBasicsStationConfigurationService stationConfigurationService;
        private readonly ILogger<ClientCertificateValidator> logger;

        public ClientCertificateValidator(IBasicsStationConfigurationService stationConfigurationService,
                                          ILogger<ClientCertificateValidator> logger)
        {
            this.stationConfigurationService = stationConfigurationService;
            this.logger = logger;
        }

        public async Task<bool> ValidateAsync(X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors _, CancellationToken token)
        {
            if (certificate is null) throw new ArgumentNullException(nameof(certificate));
            if (chain is null) throw new ArgumentNullException(nameof(chain));
            var sslErrors = false;

            var commonName = certificate.GetNameInfo(X509NameType.SimpleName, false);
            StationEui stationEui;
            try
            {
                stationEui = StationEui.Parse(commonName);
            }
            catch (FormatException)
            {
                this.logger.LogError("'{CommonName}' is not a proper StationEui field.", commonName);
                return false;
            }

            // TO DO: We should check chain properly
            foreach (var status in chain.ChainStatus)
            {
                using var scope = this.logger.BeginEuiScope(stationEui);
                this.logger.LogWarning("{Class} {Status} {StatusInformation}", nameof(ClientCertificateValidator), status.Status, status.StatusInformation);
            }

            // TO DO: Following parsing of stationEui is only working if a certificate is specifying the station eui as CN
            var thumbprints = await this.stationConfigurationService.GetAllowedClientThumbprints(stationEui, token);
            sslErrors = sslErrors || !thumbprints.Any(t => t.Equals(certificate.Thumbprint, StringComparison.OrdinalIgnoreCase));
            return !sslErrors;
        }
    }
}
