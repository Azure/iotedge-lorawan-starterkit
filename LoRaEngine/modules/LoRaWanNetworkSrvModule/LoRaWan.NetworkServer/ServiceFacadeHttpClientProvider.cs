// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Net;
    using System.Net.Http;
    using LoRaWan.Shared;

    /// <summary>
    /// Default implementation for <see cref="IServiceFacadeHttpClientProvider"/>
    /// </summary>
    public class ServiceFacadeHttpClientProvider : IServiceFacadeHttpClientProvider
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ApiVersion expectedFunctionVersion;
        private readonly Lazy<HttpClient> httpClient;

        public ServiceFacadeHttpClientProvider(NetworkServerConfiguration configuration, ApiVersion expectedFunctionVersion)
        {
            this.configuration = configuration;
            this.expectedFunctionVersion = expectedFunctionVersion;
            this.httpClient = new Lazy<HttpClient>(this.CreateHttpClient);
        }

        public HttpClient GetHttpClient() => this.httpClient.Value;

        HttpClient CreateHttpClient()
        {
            var handler = new ServiceFacadeHttpClientHandler(this.expectedFunctionVersion);

            if (!string.IsNullOrEmpty(this.configuration.HttpsProxy))
            {
                var webProxy = new WebProxy(
                    new Uri(this.configuration.HttpsProxy),
                    BypassOnLocal: false);

                handler.Proxy = webProxy;
                handler.UseProxy = true;
            }

            return new HttpClient(handler);
        }
    }
}