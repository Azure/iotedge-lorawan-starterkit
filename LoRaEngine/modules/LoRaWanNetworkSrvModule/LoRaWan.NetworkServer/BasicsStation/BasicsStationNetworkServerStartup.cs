// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Globalization;
    using LoRaTools.ADR;
    using LoRaWan;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.ApplicationInsights;
    using Prometheus;

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
            ITransportSettings[] settings = { new AmqpTransportSettings(TransportType.Amqp_Tcp_Only) };
            var loraModuleFactory = new LoRaModuleClientFactory(settings);

            var appInsightsKey = Configuration.GetValue<string>("APPINSIGHTS_INSTRUMENTATIONKEY");
            var useApplicationInsights = !string.IsNullOrEmpty(appInsightsKey);
            _ = services.AddLogging(loggingBuilder =>
                        {
                            _ = loggingBuilder.ClearProviders();
                            var logLevel = (LogLevel)int.Parse(NetworkServerConfiguration.LogLevel, CultureInfo.InvariantCulture);
                            _ = loggingBuilder.SetMinimumLevel(logLevel);
                            _ = loggingBuilder.AddLoRaConsoleLogger(c => c.LogLevel = logLevel);

                            if (useApplicationInsights)
                            {
                                _ = loggingBuilder.AddApplicationInsights(appInsightsKey)
                                                  .AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, logLevel);

                            }
                        })
                        .AddMemoryCache()
                        .AddSingleton(NetworkServerConfiguration)
                        .AddSingleton(LoRaTools.CommonAPI.ApiVersion.LatestVersion)
                        .AddSingleton<ModuleConnectionHost>()
                        .AddSingleton<IServiceFacadeHttpClientProvider, ServiceFacadeHttpClientProvider>()
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
                        .AddSingleton<IBasicsStationConfigurationService, BasicsStationConfigurationService>()
                        .AddSingleton<IClassCDeviceMessageSender, DefaultClassCDevicesMessageSender>()
                        .AddSingleton<ILoRaModuleClientFactory>(loraModuleFactory)
                        .AddSingleton<LoRaDeviceAPIServiceBase, LoRaDeviceAPIService>()
                        .AddSingleton<WebSocketWriterRegistry<StationEui, string>>()
                        .AddSingleton<IPacketForwarder, DownstreamSender>()
                        .AddTransient<ILnsProtocolMessageProcessor, LnsProtocolMessageProcessor>()
                        .AddSingleton<IConcentratorDeduplication, ConcentratorDeduplication>();

            if (useApplicationInsights)
                _ = services.AddApplicationInsightsTelemetry(appInsightsKey);
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

            // Manually set the class C as otherwise the DI fails.
            var classCMessageSender = app.ApplicationServices.GetService<IClassCDeviceMessageSender>();
            var dataHandlerImplementation = app.ApplicationServices.GetService<ILoRaDataRequestHandler>();
            dataHandlerImplementation.SetClassCMessageSender(classCMessageSender);


            _ = app.UseRouting()
                   .UseWebSockets()
                   .UseEndpoints(endpoints =>
                   {
                       _ = endpoints.MapGet(BasicsStationNetworkServer.DiscoveryEndpoint, async context =>
                           {
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDiscoveryAsync(context, context.RequestAborted);
                           });
                       _ = endpoints.MapGet($"{BasicsStationNetworkServer.DataEndpoint}/{{{BasicsStationNetworkServer.RouterIdPathParameterName}:required}}", async context =>
                           {
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDataAsync(context, context.RequestAborted);
                           });
                   });

            _ = app.UseMetricServer();
        }
    }
}
