// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System.Net.Http;
    using global::LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public sealed class LoRaApiHttpClientTests
    {
        [Fact]
        public void AddApiClient_Allows_Creation_Of_Named_Client()
        {
            // arrange
            var services = new ServiceCollection();

            // act
            var result = services.AddApiClient(new NetworkServerConfiguration(), ApiVersion.LatestVersion);

            // assert
            var httpClientFactory = result.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
            Assert.NotNull(httpClientFactory.CreateClient(LoRaApiHttpClient.Name));
        }
    }
}
