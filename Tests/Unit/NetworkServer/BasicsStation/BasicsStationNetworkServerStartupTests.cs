// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation
{
    using System;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public class BasicsStationNetworkServerStartupTests
    {
        [Fact]
        public void All_Dependencies_Are_Registered_Correctly()
        {
            // arrange
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();

            // act + assert
            var startup = new BasicsStationNetworkServerStartup(config);
            startup.ConfigureServices(services);

            services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ModuleConnectionHostIsInjectedOrNot(bool cloud_deployment)
        {
            // arrange
            Environment.SetEnvironmentVariable("CLOUD_DEPLOYMENT", cloud_deployment.ToString());
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();

            // act + assert
            var startup = new BasicsStationNetworkServerStartup(config);
            startup.ConfigureServices(services);

            var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            var result = serviceProvider.GetService<ModuleConnectionHost>();
            if (cloud_deployment)
            {
                Assert.Null(result);
            }
            else
            {
                Assert.NotNull(result);
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void EnableGatewayTrue_IoTModuleFalse_IsNotSupported(bool cloud_deployment, bool enable_gateway)
        {
            // arrange
            Environment.SetEnvironmentVariable("CLOUD_DEPLOYMENT", cloud_deployment.ToString());
            Environment.SetEnvironmentVariable("ENABLE_GATEWAY", enable_gateway.ToString());
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();

            // act + assert
            if (cloud_deployment && enable_gateway)
            {
                Assert.Throws<NotSupportedException>(() => {
                    var startup = new BasicsStationNetworkServerStartup(config);
                });
            }
            else
            {
                var startup = new BasicsStationNetworkServerStartup(config);
                startup.ConfigureServices(services);
            }

        }
    }
}
