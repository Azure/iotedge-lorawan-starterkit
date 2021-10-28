// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Globalization;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    internal sealed class BasicsStationNetworkServerStartup
    {
        public IConfiguration Configuration { get; }
        public NetworkServerConfiguration NetworkServerConfiguration { get; }

        public BasicsStationNetworkServerStartup(IConfiguration configuration)
        {
            Configuration = configuration;
            NetworkServerConfiguration = NetworkServerConfiguration.CreateFromEnvironmentVariables();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            _ = services.AddLogging(loggingBuilder =>
                        {
                            _ = loggingBuilder.SetMinimumLevel((LogLevel)int.Parse(NetworkServerConfiguration.LogLevel, CultureInfo.InvariantCulture));
                        })
                        .AddMemoryCache()
                        .AddHttpContextAccessor()
                        .AddSingleton(NetworkServerConfiguration)
                        .AddSingleton(LoRaTools.CommonAPI.ApiVersion.LatestVersion)
                        .AddSingleton<IServiceFacadeHttpClientProvider, ServiceFacadeHttpClientProvider>()
                        .AddSingleton<LoRaDeviceAPIServiceBase, LoRaDeviceAPIService>()
                        .AddSingleton<ILoRaDeviceFrameCounterUpdateStrategyProvider, LoRaDeviceFrameCounterUpdateStrategyProvider>()
                        .AddSingleton<IDeduplicationStrategyFactory, DeduplicationStrategyFactory>()
                        .AddSingleton<ILoRaADRStrategyProvider, LoRaADRStrategyProvider>()
                        .AddSingleton<ILoRAADRManagerFactory, LoRAADRManagerFactory>()
                        .AddSingleton<ILoRaDeviceClientConnectionManager, LoRaDeviceClientConnectionManager>()
                        .AddSingleton<ILoRaPayloadDecoder, LoRaPayloadDecoder>()
                        .AddSingleton<IFunctionBundlerProvider, FunctionBundlerProvider>()
                        .AddSingleton<ILoRaDataRequestHandler, DefaultLoRaDataRequestHandler>()
                        .AddSingleton<ILoRaDeviceFactory, LoRaDeviceFactory>()
                        .AddSingleton<ILoRaDeviceRegistry, LoRaDeviceRegistry>()
                        .AddSingleton<IJoinRequestMessageHandler, JoinRequestMessageHandler>()
                        .AddSingleton<IMessageDispatcher, MessageDispatcher>()
                        .AddTransient<ILnsProtocolMessageProcessor, LnsProtocolMessageProcessor>();
        }

#pragma warning disable CA1822 // Mark members as static
        // Startup class methods should not be static
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
#pragma warning restore CA1822 // Mark members as static
        {
            if (env.IsDevelopment())
            {
                _ = app.UseDeveloperExceptionPage();
            }

            // TO DO: When certificate generation is properly handled, enable https redirection
            // app.UseHttpsRedirection();

            _ = app.UseRouting()
                   .UseWebSockets()
                   .UseEndpoints(endpoints =>
                   {
                       _ = endpoints.MapGet(BasicsStationNetworkServer.DiscoveryEndpoint, async context =>
                           {
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDiscoveryAsync(context, context.RequestAborted);
                           });
                       _ = endpoints.MapGet(BasicsStationNetworkServer.DataEndpoint, async context =>
                           {
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDataAsync(context, context.RequestAborted);
                           });
                   });
        }
    }
}
