// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Net;
    using System.Net.Http;
    using LoRaTools.CommonAPI;
    using LoRaWan.Core;

    /// <summary>
    /// Default implementation for <see cref="IServiceFacadeHttpClientProvider"/>.
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
            this.httpClient = new Lazy<HttpClient>(CreateHttpClient);
        }

        public HttpClient GetHttpClient() => this.httpClient.Value;

        private HttpClient CreateHttpClient()
        {
#pragma warning disable CA2000 // Dispose objects before losing scope
            // Will be handled once we migrate to DI.
            // https://github.com/Azure/iotedge-lorawan-starterkit/issues/534
            var handler = new ServiceFacadeHttpClientHandler(this.expectedFunctionVersion);
#pragma warning restore CA2000 // Dispose objects before losing scope

            if (!string.IsNullOrEmpty(this.configuration.HttpsProxy))
            {
                var webProxy = new WebProxy(
                    new Uri(this.configuration.HttpsProxy),
                    BypassOnLocal: false);

                handler.Proxy = webProxy;
                handler.UseProxy = true;
            }

#pragma warning disable CA5399 // Definitely disable HttpClient certificate revocation list check
            // Related to: https://github.com/Azure/iotedge-lorawan-starterkit/issues/534
            // Will be resolved once we migrate to DI
            return new HttpClient(handler);
#pragma warning restore CA5399 // Definitely disable HttpClient certificate revocation list check
        }
    }
}
