// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Net;
    using System.Net.Http;
    using LoRaTools.CommonAPI;
    using LoRaWan.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Polly;

    public static class LoRaApiHttpClient
    {
        public const string Name = nameof(LoRaApiHttpClient);
    }

    public static class LoRaApiHttpClientExtensions
    {
        private const int NumberOfAggressiveRetries = 4;
        private const int NumberOfRetries = 8;
        private static readonly TimeSpan AggressiveRetryInterval = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan RetryInterval = TimeSpan.FromMilliseconds(120);

        public static IServiceCollection AddApiClient(this IServiceCollection services,
                                                      NetworkServerConfiguration configuration,
                                                      ApiVersion expectedFunctionVersion) =>
            AddApiClient(services, () =>
            {
                var handler = new ServiceFacadeHttpClientHandler(expectedFunctionVersion);

                if (!string.IsNullOrEmpty(configuration.HttpsProxy))
                {
                    var webProxy = new WebProxy(
                        new Uri(configuration.HttpsProxy),
                        BypassOnLocal: false);

                    handler.Proxy = webProxy;
                    handler.UseProxy = true;
                }

                return handler;
            });

        internal static IServiceCollection AddApiClient(this IServiceCollection services, Func<HttpMessageHandler> createHttpMessageHandler)
        {
            // This Http Client retries aggressively first to not miss the receive window if possible.
            _ = services.AddHttpClient(LoRaApiHttpClient.Name)
                        .ConfigurePrimaryHttpMessageHandler(createHttpMessageHandler)
                        .AddTransientHttpErrorPolicy(policyBuilder =>
                            policyBuilder.WaitAndRetryAsync(NumberOfRetries, i => (i <= NumberOfAggressiveRetries ? AggressiveRetryInterval : RetryInterval) * Math.Pow(2, i)));

            return services;
        }
    }
}
