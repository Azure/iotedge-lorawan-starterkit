// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer.BasicsStation
{
    using System.Collections.Generic;
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
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["HOSTNAME"] = "test",
                ["IOTHUBHOSTNAME"] = "test"
            }).Build();

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
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void ModuleConnectionHostIsInjectedOrNot(bool cloud_deployment, bool enable_gateway)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
               ["CLOUD_DEPLOYMENT"] = cloud_deployment.ToString(),
               ["ENABLE_GATEWAY"] = enable_gateway.ToString(),
               ["REDIS_CONNECTION_STRING"] = "someString",
               ["HOSTNAME"] = "test",
               ["IOTHUBHOSTNAME"] = "test"
            }).Build();

            var services = new ServiceCollection();

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
    }
}
