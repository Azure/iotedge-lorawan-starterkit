// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class BasicsStationNetworkServerTests : IAsyncLifetime
    {
        private const string CertificatePassword = "password";
        private string? passwordProtectedPfx;
        private string? notProtectedPfx;

        [Theory]
        [InlineData(true, ClientCertificateMode.NoCertificate)]
        [InlineData(true, ClientCertificateMode.AllowCertificate)]
        [InlineData(true, ClientCertificateMode.RequireCertificate)]
        [InlineData(false, ClientCertificateMode.NoCertificate)]
        [InlineData(false, ClientCertificateMode.AllowCertificate)]
        [InlineData(false, ClientCertificateMode.RequireCertificate)]
        public void GivenCertificateConfiguration_ConfigureHttpsSettings_Succeeds(bool serverPfxPasswordProtected, ClientCertificateMode clientCertificateMode)
        {
            var stationConfigurationService = Mock.Of<IBasicsStationConfigurationService>();
            var logger = Mock.Of<ILogger<ClientCertificateValidatorService>>();
            var clientCertificateValidatorService = new Mock<ClientCertificateValidatorService>(stationConfigurationService, logger);

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(x => x.GetService(typeof(ClientCertificateValidatorService)))
                           .Returns(clientCertificateValidatorService.Object);

            var httpsConnectionAdapterOptions = new HttpsConnectionAdapterOptions();

            var networkServerConfiguration = new NetworkServerConfiguration
            {
                LnsServerPfxPath = serverPfxPasswordProtected ? passwordProtectedPfx : notProtectedPfx,
                LnsServerPfxPassword = serverPfxPasswordProtected ? CertificatePassword : string.Empty,
                ClientCertificateMode = clientCertificateMode.ToString()
            };

            BasicsStationNetworkServer.ConfigureHttpsSettings(networkServerConfiguration, serviceProvider.Object, httpsConnectionAdapterOptions);

            // assert
            Assert.NotNull(httpsConnectionAdapterOptions.ServerCertificate);
            Assert.Equal(httpsConnectionAdapterOptions.ClientCertificateMode, clientCertificateMode);
            if (clientCertificateMode is not ClientCertificateMode.NoCertificate)
            {
                Assert.NotNull(httpsConnectionAdapterOptions.ClientCertificateValidation);
            } else
            {
                Assert.Null(httpsConnectionAdapterOptions.ClientCertificateValidation);
            }

        }

        private static async Task<string> CreatePfxCertificate(bool passwordProtected)
        {
            using var ecdsa = ECDsa.Create();
            var req = new CertificateRequest("cn=foobar", ecdsa, HashAlgorithmName.SHA256);
            var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));

            var tempFileName = Path.GetTempFileName();
            // Create PFX (PKCS #12) with private key
            await File.WriteAllBytesAsync(tempFileName, cert.Export(X509ContentType.Pfx, passwordProtected ? CertificatePassword : null));
            return tempFileName;
        }

        public async Task InitializeAsync()
        {
            this.passwordProtectedPfx = await CreatePfxCertificate(true);
            this.notProtectedPfx = await CreatePfxCertificate(false);
        }

        public Task DisposeAsync()
        {
            if (this.passwordProtectedPfx is { }) File.Delete(this.passwordProtectedPfx);
            if (this.notProtectedPfx is { }) File.Delete(this.notProtectedPfx);

            return Task.CompletedTask;
        }
    }
}
