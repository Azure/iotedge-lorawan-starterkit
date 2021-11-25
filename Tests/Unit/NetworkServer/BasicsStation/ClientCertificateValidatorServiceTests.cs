// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
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
        private string clientCertificateWithEuiPath;
        private string clientCertificateWithInvalidEuiPath;

        public ClientCertificateValidatorServiceTests()
        {
            stationConfigService = new Mock<IBasicsStationConfigurationService>();
            logger = new Mock<ILogger<ClientCertificateValidatorService>>();
            clientCertValidatorSvc = new ClientCertificateValidatorService(stationConfigService.Object, logger.Object);
        }

        [Fact]
        public async Task ValidateAsync_Throws_WithoutCertificate()
        {
            using var chain = X509Chain.Create();
            await Assert.ThrowsAsync<ArgumentNullException>(() => this.clientCertValidatorSvc.ValidateAsync(null, chain, System.Net.Security.SslPolicyErrors.None, default));
        }

        [Fact]
        public async Task ValidateAsync_Throws_WithoutChain()
        {
            using var cert = new X509Certificate2(clientCertificateWithEuiPath);
            await Assert.ThrowsAsync<ArgumentNullException>(() => this.clientCertValidatorSvc.ValidateAsync(cert, null, System.Net.Security.SslPolicyErrors.None, default));
        }

        [Fact]
        public async Task ValidateAsync_ReturnsTrue_WithExpectedThumbprint()
        {
            using var cert = new X509Certificate2(clientCertificateWithEuiPath);
            using var chain = X509Chain.Create();

            stationConfigService.Setup(x => x.GetAllowedClientThumbprintsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new[] { cert.Thumbprint }));

            var result = await this.clientCertValidatorSvc.ValidateAsync(cert, chain, System.Net.Security.SslPolicyErrors.None, default);

            Assert.True(result);
        }

        [Fact]
        public async Task ValidateAsync_ReturnsFalse_WithNonExpectedThumbprint()
        {
            using var cert = new X509Certificate2(clientCertificateWithEuiPath);
            using var chain = X509Chain.Create();

            stationConfigService.Setup(x => x.GetAllowedClientThumbprintsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new[] { "AnotherThumbprint" }));

            var result = await this.clientCertValidatorSvc.ValidateAsync(cert, chain, System.Net.Security.SslPolicyErrors.None, default);

            Assert.False(result);
        }

        [Fact]
        public async Task ValidateAsync_ReturnsFalse_WithInvalidCNCertificate()
        {
            using var cert = new X509Certificate2(clientCertificateWithInvalidEuiPath);
            using var chain = X509Chain.Create();

            stationConfigService.Setup(x => x.GetAllowedClientThumbprintsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new[] { cert.Thumbprint }));

            var result = await this.clientCertValidatorSvc.ValidateAsync(cert, chain, System.Net.Security.SslPolicyErrors.None, default);

            Assert.False(result);
            Assert.Contains(this.logger.Invocations, i => i.Arguments.Any(a => a.ToString().Contains(InvalidStationEui, System.StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public async Task ValidateAsync_ReturnsTrue_WithUntrustedRoot()
        {
            using var cert = new X509Certificate2(clientCertificateWithEuiPath);
            using var chain = X509Chain.Create();
            chain.Build(cert);

            stationConfigService.Setup(x => x.GetAllowedClientThumbprintsAsync(It.IsAny<StationEui>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(new[] { cert.Thumbprint }));

            var result = await this.clientCertValidatorSvc.ValidateAsync(cert, chain, System.Net.Security.SslPolicyErrors.None, default);

            Assert.True(result);
            Assert.Contains(this.logger.Invocations, i => i.Arguments.Any(a => a.ToString().Contains(X509ChainStatusFlags.UntrustedRoot.ToString(), System.StringComparison.OrdinalIgnoreCase)));
        }



        public Task DisposeAsync()
        {
            File.Delete(this.clientCertificateWithEuiPath);
            File.Delete(this.clientCertificateWithInvalidEuiPath);
            return Task.CompletedTask;
        }

        public async Task InitializeAsync()
        {
            this.clientCertificateWithEuiPath = await BasicsStationNetworkServerTests.CreatePfxCertificate(false);
            this.clientCertificateWithInvalidEuiPath = await BasicsStationNetworkServerTests.CreatePfxCertificate(false, InvalidStationEui);
        }
    }
}
