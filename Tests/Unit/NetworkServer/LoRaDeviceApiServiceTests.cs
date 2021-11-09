// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Linq;
    using LoRaWan.NetworkServer;
    using Moq;
    using Xunit;

    public class LoRaDeviceApiServiceTests
    {
        [Theory]
        [InlineData("https://aka.ms", "api/A?code=B", "https://aka.ms/api/A?code=B")]
        [InlineData("https://aka.ms/", "api/A?code=B", "https://aka.ms/api/A?code=B")]
        [InlineData("https://aka.ms/api", "A?code=B", "https://aka.ms/api/A?code=B")]
        [InlineData("https://aka.ms/api/", "A?code=B", "https://aka.ms/api/A?code=B")]
        public void GetFullUri_Success(string basePath, string relativePath, string expected)
        {
            // arrange
            var sut = Setup(basePath);

            // act
            var result = sut.GetFullUri(relativePath);

            // assert
            Assert.Equal(expected, result.ToString());
        }

        [Theory]
        [InlineData("https://aka.ms/api/", "A", "https://aka.ms/api/A?foo=fooVal&bar=barVal", "foo", "fooVal", "bar", "barVal")]
        [InlineData("https://aka.ms/api", "A", "https://aka.ms/api/A?foo=fooVal&bar=barVal", "foo", "fooVal", "bar", "barVal")]
        [InlineData("https://aka.ms/api", "A", "https://aka.ms/api/A?foo=fooVal", "foo", "fooVal", "bar", null)]
        [InlineData("https://aka.ms/api", "A", "https://aka.ms/api/A", "foo", null, "bar", null)]
        public void BuildUri_Success(string basePath, string relativePath, string expected, params string[] queryParams)
        {
            // arrange
            var sut = Setup(basePath);

            // act
            var queryParamsDict =
                queryParams.Zip(queryParams[1..])
                           .Where((_, i) => i % 2 == 0)
                           .ToDictionary(z => z.First, z => z.Second);
            var result = sut.BuildUri(relativePath, queryParamsDict);

            // assert
            Assert.Equal(expected, result.ToString());
        }

        private static LoRaDeviceAPIService Setup(string basePath) =>
            new LoRaDeviceAPIService(new NetworkServerConfiguration { FacadeServerUrl = new Uri(basePath) },
                                     new Mock<IServiceFacadeHttpClientProvider>().Object);
    }
}
