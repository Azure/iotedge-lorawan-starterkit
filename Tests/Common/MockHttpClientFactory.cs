// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Net.Http;

    public sealed class MockHttpClientFactory : IHttpClientFactory, IDisposable
    {
        public HttpClient HttpClient { get; }

        public MockHttpClientFactory() : this(new HttpClient()) { }

        public MockHttpClientFactory(HttpClient httpClient) => HttpClient = httpClient;

        /// <summary>
        /// Creates an IHttpClientFactory which always returns an HttpClient containing only the httpMessageHandler parameter.
        /// </summary>
        /// <param name="httpMessageHandler"></param>
        public MockHttpClientFactory(HttpMessageHandler httpMessageHandler)
            : this(new HttpClient(httpMessageHandler)) { }

        public HttpClient CreateClient(string name) => HttpClient;

        public void Dispose() => HttpClient.Dispose();
    }
}
