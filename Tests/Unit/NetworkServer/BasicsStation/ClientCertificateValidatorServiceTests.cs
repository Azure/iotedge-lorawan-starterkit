// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class ClientCertificateValidatorServiceTests : IAsyncLifetime
    {
        private readonly ClientCertificateValidatorService clientCertValidatorSvc;
        private readonly Mock<IBasicsStationConfigurationService> stationConfigService;
        private readonly Mock<ILogger<ClientCertificateValidatorService>> logger;
        private const string InvalidStationEui = "NotAStationEui";
        private string? clientCertificateWithEuiPath;

        public ClientCertificateValidatorServiceTests()
        {
            this.stationConfigService = new Mock<IBasicsStationConfigurationService>();
            this.logger = new Mock<ILogger<ClientCertificateValidatorService>>();
            this.clientCertValidatorSvc = new ClientCertificateValidatorService(this.stationConfigService.Object, new RegistryMetricTagBag(new NetworkServerConfiguration { GatewayID = "foogateway" }), this.logger.Object);
        }

        [Fact]
        public async Task ValidateAsync_Throws_WithoutChain()
        {
            if (string.IsNullOrEmpty(this.clientCertificateWithEuiPath))
            {
                throw new InvalidOperationException("Client certificate was not properly initialized.");
            }

            using var cert = new X509Certificate2(this.clientCertificateWithEuiPath);
            _ = await Assert.ThrowsAsync<ArgumentNullException>(() => this.clientCertValidatorSvc.ValidateAsync(cert, null, System.Net.Security.SslPolicyErrors.None, default));
        }

        [Fact]
        public async Task ValidateAsync_ReturnsTrue_WithExpectedThumbprint()
        {
            if (string.IsNullOrEmpty(this.clientCertificateWithEuiPath))
            {
                throw new InvalidOperationException("Client certificate was not properly initialized.");
            }

            using var cert = new X509Certificate2(this.clientCertificateWithEuiPath);
            using var chain = X509Chain.Create();

            _ = this.stationConfigService.Setup(x => x.GetAllowedClientThumbprintsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                         .Returns(Task.FromResult(new[] { cert.Thumbprint }));

            var result = await this.clientCertValidatorSvc.ValidateAsync(cert, chain, System.Net.Security.SslPolicyErrors.None, default);

            Assert.True(result);
        }

        [Fact]
        public async Task ValidateAsync_ReturnsFalse_WithNonExpectedThumbprint()
        {
            if (string.IsNullOrEmpty(this.clientCertificateWithEuiPath))
            {
                throw new InvalidOperationException("Client certificate was not properly initialized.");
            }

            using var cert = new X509Certificate2(this.clientCertificateWithEuiPath);
            using var chain = X509Chain.Create();

            _ = this.stationConfigService.Setup(x => x.GetAllowedClientThumbprintsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                         .Returns(Task.FromResult(new[] { "AnotherThumbprint" }));

            var result = await this.clientCertValidatorSvc.ValidateAsync(cert, chain, System.Net.Security.SslPolicyErrors.None, default);

            Assert.False(result);
        }

        [Fact]
        public async Task ValidateAsync_ReturnsFalse_WithInvalidCNCertificate()
        {
            var clientCertificateWithInvalidEuiPath = await BasicsStationNetworkServerTests.CreatePfxCertificateAsync(false, InvalidStationEui);
            try
            {
                using var cert = new X509Certificate2(clientCertificateWithInvalidEuiPath);
                using var chain = X509Chain.Create();

                _ = this.stationConfigService.Setup(x => x.GetAllowedClientThumbprintsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                             .Returns(Task.FromResult(new[] { cert.Thumbprint }));

                var result = await this.clientCertValidatorSvc.ValidateAsync(cert, chain, System.Net.Security.SslPolicyErrors.None, default);

                Assert.False(result);
                Assert.Contains(this.logger.Invocations, i => i.Arguments.Any(a => a.ToString()!.Contains(InvalidStationEui, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                if (!string.IsNullOrEmpty(clientCertificateWithInvalidEuiPath))
                    File.Delete(clientCertificateWithInvalidEuiPath);
            }

        }

        [Fact]
        public async Task ValidateAsync_ReturnsTrue_WithUntrustedRoot()
        {
            if (string.IsNullOrEmpty(this.clientCertificateWithEuiPath))
            {
                throw new InvalidOperationException("Client certificate was not properly initialized.");
            }

            using var cert = new X509Certificate2(this.clientCertificateWithEuiPath);
            using var chain = X509Chain.Create();
            _ = chain.Build(cert);

            _ = this.stationConfigService.Setup(x => x.GetAllowedClientThumbprintsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                                         .Returns(Task.FromResult(new[] { cert.Thumbprint }));

            var result = await this.clientCertValidatorSvc.ValidateAsync(cert, chain, System.Net.Security.SslPolicyErrors.None, default);

            Assert.True(result);
            Assert.Contains(this.logger.Invocations, i => i.Arguments.Any(a => a.ToString()!.Contains(X509ChainStatusFlags.UntrustedRoot.ToString(), StringComparison.OrdinalIgnoreCase)));
        }



        public Task DisposeAsync()
        {
            if (!string.IsNullOrEmpty(this.clientCertificateWithEuiPath))
                File.Delete(this.clientCertificateWithEuiPath);
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            this.clientCertificateWithEuiPath = await BasicsStationNetworkServerTests.CreatePfxCertificateAsync(false);
        }
    }
}
