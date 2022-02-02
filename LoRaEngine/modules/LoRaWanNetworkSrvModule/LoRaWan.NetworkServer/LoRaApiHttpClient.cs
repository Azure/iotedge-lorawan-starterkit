// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Net;
    using LoRaTools.CommonAPI;
    using LoRaWan.Core;
    using Microsoft.Extensions.DependencyInjection;

    public static class LoRaApiHttpClient
    {
        public const string Name = nameof(LoRaApiHttpClient);
    }

    public static class LoRaApiHttpClientExtensions
    {
        public static IServiceCollection AddApiClient(this IServiceCollection services,
                                                      NetworkServerConfiguration configuration,
                                                      ApiVersion expectedFunctionVersion)
        {
            _ = services.AddHttpClient(LoRaApiHttpClient.Name)
                        .ConfigurePrimaryHttpMessageHandler(() =>
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

            return services;
        }
    }
}
