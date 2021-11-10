// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServerTests.BasicsStation
{
    using System.Collections.Generic;
    using LoRaWan.NetworkServer.BasicsStation;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public class BasicsStationNetworkServerStartupTests
    {
        private class FunctionsHostBuilderWrapper : IFunctionsHostBuilder
        {
            public FunctionsHostBuilderWrapper(IServiceCollection services)
            {
                Services = services;
            }
            public IServiceCollection Services { get; }
        }

        [Fact]
        public void All_Dependencies_Are_Registered_Correctly()
        {
            var services = new ServiceCollection();
            var functionsHostWrapper = new FunctionsHostBuilderWrapper(services);

            var configuration = new Dictionary<string, string>
            {
            };

            var config = new ConfigurationBuilder().AddInMemoryCollection(configuration)
                                                   .Build();

            services.AddSingleton<IConfiguration>(config);

            var startup = new BasicsStationNetworkServerStartup(config);

            startup.ConfigureServices(functionsHostWrapper.Services);

            services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });
        }
    }
}
