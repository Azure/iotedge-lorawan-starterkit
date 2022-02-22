// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Linq;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer.Logger;
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

            // Logging any chain related issue that is causing verification to fail
            if (chain.ChainStatus.Any(s => s.Status != X509ChainStatusFlags.NoError))
            {
                foreach (var status in chain.ChainStatus)
                {
                    this.logger.LogError("{Status} {StatusInformation}", status.Status, status.StatusInformation);
                }
                this.logger.LogError("Some errors were found in the chain.");
                return false;
            }

            // Additional validation is done on certificate thumprint
            try
            {
                var thumbprints = await this.stationConfigurationService.GetAllowedClientThumbprintsAsync(stationEui, token);
                var thumbprintFound = thumbprints.Any(t => t.Equals(certificate.Thumbprint, StringComparison.OrdinalIgnoreCase));
                if (!thumbprintFound)
                    this.logger.LogDebug($"'{certificate.Thumbprint}' was not found as allowed thumbprint for {stationEui}");
                return thumbprintFound;
            }
            catch (Exception ex) when (ExceptionFilterUtility.False(() => this.logger.LogError(ex, "An exception occurred while processing requests: {Exception}.", ex)))
            {
                return false;
            }
        }
    }
}
