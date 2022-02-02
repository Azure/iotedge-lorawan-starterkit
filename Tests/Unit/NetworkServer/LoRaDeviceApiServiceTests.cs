// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
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

        [Fact]
        public async Task SearchByEuiAsync_StationEui_Is_Compatible_With_Contract()
        {
            // arrange
            using var content = new StringContent(SearchByDevEuiContract.Response, Encoding.UTF8, "application/json");
            using var httpClientFactoryMock = SetupHttpClientFactoryMock(content);
            var subject = Setup(httpClientFactoryMock.Value.Object);

            // act
            var result = await subject.GetPrimaryKeyByEuiAsync(new StationEui(1));

            // assert
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(SearchByDevEuiContract.PrimaryKey)), result);
        }

        [Fact]
        public async Task SearchByEuiAsync_DevEui_Is_Compatible_With_Contract()
        {
            // arrange
            using var content = new StringContent(SearchByDevEuiContract.Response, Encoding.UTF8, "application/json");
            using var httpClientFactoryMock = SetupHttpClientFactoryMock(content);
            var subject = Setup(httpClientFactoryMock.Value.Object);

            // act
            var result = await subject.GetPrimaryKeyByEuiAsync(new DevEui(1));

            // assert
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes(SearchByDevEuiContract.PrimaryKey)), result);
        }

        [Theory]
        [InlineData("null", null)]
        [InlineData(@"""""", null)]
        [InlineData(@"{""primaryKey"":""1234""}", "1234")]
        [InlineData(@"{""PrimaryKey"":""1234""}", "1234")]
        [InlineData(@"{""primaryKey"":""""}", "")]
        [InlineData("{}", null)]
        [InlineData("", null)]
        public async Task SearchByEuiAsync_DevEui_Parses_Json(string json, string expected)
        {
            // arrange
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var httpClientFactoryMock = SetupHttpClientFactoryMock(content);
            var subject = Setup(httpClientFactoryMock.Value.Object);

            // act
            var result = await subject.GetPrimaryKeyByEuiAsync(new DevEui(1));

            // assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task FetchStationFirmwareAsync_Returns_Stream_And_Content_Length()
        {
            // arrange
            var contentBytes = Encoding.UTF8.GetBytes("Test");
            using var byteArrayContent = new ByteArrayContent(contentBytes);
            using var httpClientFactoryMock = SetupHttpClientFactoryMock(byteArrayContent);
            var subject = Setup(httpClientFactoryMock.Value.Object);

            // act
            var content = await subject.FetchStationFirmwareAsync(new StationEui(ulong.MaxValue), CancellationToken.None);

            // assert
            var expectedBytes = new byte[contentBytes.Length];
            await (await content.ReadAsStreamAsync()).ReadAsync(expectedBytes, CancellationToken.None);
            Assert.Equal(content.Headers.ContentLength, contentBytes.Length);
            Assert.Equal(expectedBytes, contentBytes);
        }

        private static DisposableValue<Mock<IHttpClientFactory>> SetupHttpClientFactoryMock(HttpContent content)
        {
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
#pragma warning disable CA2000 // Dispose objects before losing scope
            // ownership is transferred to return value.
            var httpHandlerMock = new HttpMessageHandlerMock();
            var httpClient = new HttpClient(httpHandlerMock);
#pragma warning restore CA2000 // Dispose objects before losing scope
            httpHandlerMock.SetupHandler(r => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = content
            });
            httpClientFactoryMock.Setup(f => f.CreateClient(LoRaApiHttpClient.Name)).Returns(httpClient);
            return new DisposableValue<Mock<IHttpClientFactory>>(httpClientFactoryMock, () => { httpHandlerMock.Dispose(); httpClient.Dispose(); });
        }

        private static LoRaDeviceAPIService Setup(string basePath) =>
            new LoRaDeviceAPIService(new NetworkServerConfiguration { FacadeServerUrl = new Uri(basePath) },
                                     new Mock<IHttpClientFactory>().Object,
                                     NullLogger<LoRaDeviceAPIService>.Instance,
                                     TestMeter.Instance);

        private static LoRaDeviceAPIService Setup(IHttpClientFactory httpClientFactory) =>
            new LoRaDeviceAPIService(new NetworkServerConfiguration { FacadeServerUrl = new Uri("https://aka.ms/api/") },
                                     httpClientFactory,
                                     NullLogger<LoRaDeviceAPIService>.Instance,
                                     TestMeter.Instance);
    }
}
