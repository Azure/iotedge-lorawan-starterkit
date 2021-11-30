// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using Logger;
    using LoRaTools.ADR;
    using LoRaWan;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using Microsoft.ApplicationInsights;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Https;
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
                            var logLevel = int.TryParse(NetworkServerConfiguration.LogLevel, NumberStyles.Integer, CultureInfo.InvariantCulture, out var logLevelNum)
                                ? (LogLevel)logLevelNum is var level && Enum.IsDefined(typeof(LogLevel), level) ? level : throw new InvalidCastException()
                                : Enum.Parse<LogLevel>(NetworkServerConfiguration.LogLevel, true);

                            _ = loggingBuilder.SetMinimumLevel(logLevel);
                            _ = loggingBuilder.AddLoRaConsoleLogger(c => c.LogLevel = logLevel);

                            if (NetworkServerConfiguration.LogToTcp)
                            {
                                _ = loggingBuilder.AddTcpLogger(new TcpLoggerConfiguration(logLevel, NetworkServerConfiguration.LogToTcpAddress,
                                                                                           NetworkServerConfiguration.LogToTcpPort,
                                                                                           NetworkServerConfiguration.GatewayID));
                            }

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
                        .AddTransient<ICupsProtocolMessageProcessor, CupsProtocolMessageProcessor>()
                        .AddSingleton(typeof(IConcentratorDeduplication<>), typeof(ConcentratorDeduplication<>))
                        .AddSingleton(new RegistryMetricTagBag())
                        .AddSingleton(_ => new Meter(MetricRegistry.Namespace, MetricRegistry.Version))
                        .AddHostedService(sp =>
                            new MetricExporterHostedService(
                                new CompositeMetricExporter(useApplicationInsights ? new ApplicationInsightsMetricExporter(sp.GetRequiredService<TelemetryClient>(),
                                                                                                                           sp.GetRequiredService<RegistryMetricTagBag>()) : null,
                                                            new PrometheusMetricExporter(sp.GetRequiredService<RegistryMetricTagBag>()))))
                        .AddSingleton(_ => new Meter(MetricRegistry.Namespace, MetricRegistry.Version));

            if (useApplicationInsights)
                _ = services.AddApplicationInsightsTelemetry(appInsightsKey);

            if (NetworkServerConfiguration.ClientCertificateMode is not ClientCertificateMode.NoCertificate)
                _ = services.AddSingleton<IClientCertificateValidatorService, ClientCertificateValidatorService>();
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
                               if (context.Connection.LocalPort != BasicsStationNetworkServer.LnsPort)
                               {
                                   context.Response.StatusCode = (int)System.Net.HttpStatusCode.MethodNotAllowed;
                                   return;
                               }
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDiscoveryAsync(context, context.RequestAborted);
                           });
                       _ = endpoints.MapGet($"{BasicsStationNetworkServer.DataEndpoint}/{{{BasicsStationNetworkServer.RouterIdPathParameterName}:required}}", async context =>
                           {
                               if (context.Connection.LocalPort != BasicsStationNetworkServer.LnsPort)
                               {
                                   context.Response.StatusCode = (int)System.Net.HttpStatusCode.MethodNotAllowed;
                                   return;
                               }
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDataAsync(context, context.RequestAborted);
                           });
                       _ = endpoints.MapPost(BasicsStationNetworkServer.UpdateInfoEndpoint, async context =>
                           {
                               if (context.Connection.LocalPort != BasicsStationNetworkServer.CupsPort)
                               {
                                   context.Response.StatusCode = (int)System.Net.HttpStatusCode.MethodNotAllowed;
                                   return;
                               }
                               var cupsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ICupsProtocolMessageProcessor>();
                               await cupsProtocolMessageProcessor.HandleUpdateInfoAsync(context, context.RequestAborted);
                           });
                   });

            _ = app.UseMetricServer();
        }
    }
}
