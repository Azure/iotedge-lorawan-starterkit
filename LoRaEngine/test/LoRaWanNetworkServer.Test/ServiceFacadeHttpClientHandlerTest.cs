// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Shared;
    using Xunit;

    public class ServiceFacadeHttpClientHandlerTest
    {
        private HttpResponseMessage fakeHttpResponseMessage;
        private Uri fakeRequestedUri;

        private Task<HttpResponseMessage> FakeHandler(HttpRequestMessage httpRequestMessage, CancellationToken cts)
        {
            this.fakeRequestedUri = httpRequestMessage.RequestUri;
            return Task.FromResult(this.fakeHttpResponseMessage);
        }

        [Fact]
        public async Task When_Function_Returns_No_Version_Should_Return_Bad_Request()
        {
            var target = new ServiceFacadeHttpClientHandler(ApiVersion.Version_2018_12_16_Preview, this.FakeHandler);
            this.fakeHttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("100"),
            };

            var httpClient = new HttpClient(target);
            var response = await httpClient.GetAsync("https://mytest.test.com/api/Function1?code=aaabbbb");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Version mismatch (expected: 2018-12-16-preview, function version: 0.2 or earlier), ensure you have the latest version deployed", response.ReasonPhrase);
            Assert.Equal("https://mytest.test.com/api/Function1?code=aaabbbb&api-version=2018-12-16-preview", this.fakeRequestedUri.ToString());
        }

        [Fact]
        public async Task When_Function_Returns_Same_Version_Should_Return_OK()
        {
            var target = new ServiceFacadeHttpClientHandler(ApiVersion.Version_2018_12_16_Preview, this.FakeHandler);
            this.fakeHttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("100"),
            };
            this.fakeHttpResponseMessage.Headers.Add(ApiVersion.HttpHeaderName, ApiVersion.Version_2018_12_16_Preview.Version);

            var httpClient = new HttpClient(target);
            var response = await httpClient.GetAsync("https://mytest.test.com/api/Function1?code=aaabbbb");
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("https://mytest.test.com/api/Function1?code=aaabbbb&api-version=2018-12-16-preview", this.fakeRequestedUri.ToString());
            Assert.Equal("100", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task When_Function_Uri_Has_No_Parameter_Adds_Question_Mark()
        {
            var target = new ServiceFacadeHttpClientHandler(ApiVersion.Version_2018_12_16_Preview, this.FakeHandler);
            this.fakeHttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("100"),
            };
            this.fakeHttpResponseMessage.Headers.Add(ApiVersion.HttpHeaderName, ApiVersion.Version_2018_12_16_Preview.Version);

            var httpClient = new HttpClient(target);
            var response = await httpClient.GetAsync("https://mytest.test.com/api/Function1");
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("https://mytest.test.com/api/Function1?api-version=2018-12-16-preview", this.fakeRequestedUri.ToString());
            Assert.Equal("100", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task When_Function_Returns_Unknown_Newer_Version_Should_Return_OK()
        {
            var target = new ServiceFacadeHttpClientHandler(ApiVersion.Version_2018_12_16_Preview, this.FakeHandler);
            this.fakeHttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("100"),
            };
            this.fakeHttpResponseMessage.Headers.Add(ApiVersion.HttpHeaderName, "2018-12-30-preview");

            var httpClient = new HttpClient(target);
            var response = await httpClient.GetAsync("https://mytest.test.com/api/Function1?code=aaabbbb");
            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("https://mytest.test.com/api/Function1?code=aaabbbb&api-version=2018-12-16-preview", this.fakeRequestedUri.ToString());
            Assert.Equal("100", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task When_Function_Returns_Known_Older_Version_Should_Return_Bad_Request()
        {
            var target = new ServiceFacadeHttpClientHandler(ApiVersion.Version_2019_02_12_Preview, this.FakeHandler);
            this.fakeHttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("100"),
            };
            this.fakeHttpResponseMessage.Headers.Add(ApiVersion.HttpHeaderName, ApiVersion.Version_2018_12_16_Preview.Version);

            var httpClient = new HttpClient(target);
            var response = await httpClient.GetAsync("https://mytest.test.com/api/Function1?code=aaabbbb");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Version mismatch (expected: 2019-02-12-preview, function version: 2018-12-16-preview), ensure you have the latest version deployed", response.ReasonPhrase);
            Assert.Equal("https://mytest.test.com/api/Function1?code=aaabbbb&api-version=2019-02-12-preview", this.fakeRequestedUri.ToString());
        }

        [Fact]
        public async Task When_Function_Returns_Unknown_Older_Version_Should_Return_Bad_Request()
        {
            var target = new ServiceFacadeHttpClientHandler(ApiVersion.Version_2019_02_12_Preview, this.FakeHandler);
            this.fakeHttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("100"),
            };
            this.fakeHttpResponseMessage.Headers.Add(ApiVersion.HttpHeaderName, "2018-01-01-preview");

            var httpClient = new HttpClient(target);
            var response = await httpClient.GetAsync("https://mytest.test.com/api/Function1?code=aaabbbb");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Version mismatch (expected: 2019-02-12-preview, function version: 2018-01-01-preview), ensure you have the latest version deployed", response.ReasonPhrase);
            Assert.Equal("https://mytest.test.com/api/Function1?code=aaabbbb&api-version=2019-02-12-preview", this.fakeRequestedUri.ToString());
        }

        [Fact]
        public async Task When_Function_Returns_Error_Does_Not_Check_Version_Compatibility()
        {
            var target = new ServiceFacadeHttpClientHandler(ApiVersion.Version_2019_02_12_Preview, this.FakeHandler);
            this.fakeHttpResponseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("100"),
                ReasonPhrase = "Internal Server Error",
            };
            this.fakeHttpResponseMessage.Headers.Add(ApiVersion.HttpHeaderName, "2018-01-01-preview");

            var httpClient = new HttpClient(target);
            var response = await httpClient.GetAsync("https://mytest.test.com/api/Function1?code=aaabbbb");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("Internal Server Error", response.ReasonPhrase);
            Assert.Equal("https://mytest.test.com/api/Function1?code=aaabbbb&api-version=2019-02-12-preview", this.fakeRequestedUri.ToString());
        }

        [Fact]
        public async Task When_Caller_Is_2018_12_16_And_Function_2019_02_12_Should_Return_Bad_Request()
        {
            var target = new ServiceFacadeHttpClientHandler(ApiVersion.Version_2018_12_16_Preview, this.FakeHandler);
            this.fakeHttpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("100"),
            };
            this.fakeHttpResponseMessage.Headers.Add(ApiVersion.HttpHeaderName, ApiVersion.Version_2019_02_12_Preview.Version);

            var httpClient = new HttpClient(target);
            var response = await httpClient.GetAsync("https://mytest.test.com/api/Function1?code=aaabbbb");
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("Version mismatch (expected: 2018-12-16-preview, function version: 2019-02-12-preview), ensure you have the latest version deployed", response.ReasonPhrase);
            Assert.Equal("https://mytest.test.com/api/Function1?code=aaabbbb&api-version=2018-12-16-preview", this.fakeRequestedUri.ToString());
        }
    }
}
