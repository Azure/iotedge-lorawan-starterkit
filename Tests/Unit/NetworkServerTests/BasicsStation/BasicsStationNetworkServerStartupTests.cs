// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests.BasicsStation
{
    using LoRaWan.NetworkServer.BasicsStation;
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
    }
}
