// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    internal sealed class ClientCertificateValidatorService : IClientCertificateValidatorService
    {
        private readonly IBasicsStationConfigurationService stationConfigurationService;
        private readonly ILogger<ClientCertificateValidatorService> logger;

        public ClientCertificateValidatorService(IBasicsStationConfigurationService stationConfigurationService,
                                                 ILogger<ClientCertificateValidatorService> logger)
        {
            this.stationConfigurationService = stationConfigurationService;
            this.logger = logger;
        }

        public async Task<bool> ValidateAsync(X509Certificate2 certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors, CancellationToken token)
        {
            if (certificate is null) throw new ArgumentNullException(nameof(certificate));
            if (chain is null) throw new ArgumentNullException(nameof(chain));

            var commonName = certificate.GetNameInfo(X509NameType.SimpleName, false);
            var regex = Regex.Match(commonName, "([a-fA-F0-9]{2}[-:]?){8}");
            var parseSuccess = StationEui.TryParse(regex.Value, out var stationEui);

            if (!parseSuccess)
            {
                this.logger.LogError("Could not find a possible StationEui in '{CommonName}'.", commonName);
                return false;
            }

            using var scope = this.logger.BeginEuiScope(stationEui);

            // Logging any chain related issue, but not failing on it.
            foreach (var status in chain.ChainStatus)
            {
                this.logger.LogDebug("{Status} {StatusInformation}", status.Status, status.StatusInformation);
            }

            // Only validation is currently done on thumprint
            var thumbprints = await this.stationConfigurationService.GetAllowedClientThumbprintsAsync(stationEui, token);
            var thumbprintFound = thumbprints.Any(t => t.Equals(certificate.Thumbprint, StringComparison.OrdinalIgnoreCase));
            if (!thumbprintFound)
                this.logger.LogDebug($"'{certificate.Thumbprint}' was not found as allowed thumbprint for {stationEui}");
            return thumbprintFound;
        }
    }
}
