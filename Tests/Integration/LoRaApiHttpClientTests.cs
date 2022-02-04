// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public sealed class LoRaApiHttpClientTests
    {
        [Fact]
        public async Task AddApiClient_HttpClient_Retries_Eight_Times()
        {
            // arrange
            var count = 0;
            using var handler = new HttpMessageHandlerMock();
            handler.SetupHandler(_ =>
            {
                ++count;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });
            var client = new ServiceCollection().AddApiClient(() => handler)
                                                .BuildServiceProvider()
                                                .GetRequiredService<IHttpClientFactory>()
                                                .CreateClient(LoRaApiHttpClient.Name);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://inexistenturlfoobar.ms"));

            // act + assert
            var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(9, count);
        }
    }
}
