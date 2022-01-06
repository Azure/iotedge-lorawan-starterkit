// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Logger;
    using LoRaTools.ADR;
    using LoRaWan;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.BasicsStation.ModuleConnection;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
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
                            if (NetworkServerConfiguration.LogToHub)
                                _ = loggingBuilder.AddIotHubLogger(c => c.LogLevel = logLevel);

                            if (useApplicationInsights)
                            {
                                _ = loggingBuilder.AddApplicationInsights(appInsightsKey)
                                                  .AddFilter<ApplicationInsightsLoggerProvider>(string.Empty, logLevel);
                                _ = services.AddSingleton<ITelemetryInitializer>(_ => new TelemetryInitializer(NetworkServerConfiguration));
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
                        .AddSingleton<LoRaDeviceCache>()
                        .AddSingleton(new LoRaDeviceCacheOptions { MaxUnobservedLifetime = TimeSpan.FromDays(10), RefreshInterval = TimeSpan.FromDays(2), ValidationInterval = TimeSpan.FromMinutes(10) })
                        .AddTransient<ILnsProtocolMessageProcessor, LnsProtocolMessageProcessor>()
                        .AddTransient<ICupsProtocolMessageProcessor, CupsProtocolMessageProcessor>()
                        .AddSingleton<IConcentratorDeduplication, ConcentratorDeduplication>()
                        .AddSingleton(new RegistryMetricTagBag(NetworkServerConfiguration))
                        .AddSingleton(_ => new Meter(MetricRegistry.Namespace, MetricRegistry.Version))
                        .AddHostedService(sp =>
                            new MetricExporterHostedService(
                                new CompositeMetricExporter(useApplicationInsights ? new ApplicationInsightsMetricExporter(sp.GetRequiredService<TelemetryClient>(),
                                                                                                                           sp.GetRequiredService<RegistryMetricTagBag>(),
                                                                                                                           sp.GetRequiredService<ILogger<ApplicationInsightsMetricExporter>>()) : null,
                                                            new PrometheusMetricExporter(sp.GetRequiredService<RegistryMetricTagBag>(), sp.GetRequiredService<ILogger<PrometheusMetricExporter>>()))))
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
                       _ = endpoints.MapMetrics();

                       Map(HttpMethod.Get, BasicsStationNetworkServer.DiscoveryEndpoint,
                           context => context.Request.Host.Port is BasicsStationNetworkServer.LnsPort or BasicsStationNetworkServer.LnsSecurePort,
                           (ILnsProtocolMessageProcessor processor) => processor.HandleDiscoveryAsync);

                       Map(HttpMethod.Get, $"{BasicsStationNetworkServer.DataEndpoint}/{{{BasicsStationNetworkServer.RouterIdPathParameterName}:required}}",
                           context => context.Request.Host.Port is BasicsStationNetworkServer.LnsPort or BasicsStationNetworkServer.LnsSecurePort,
                           (ILnsProtocolMessageProcessor processor) => processor.HandleDataAsync);

                       Map(HttpMethod.Post, BasicsStationNetworkServer.UpdateInfoEndpoint,
                           context => context.Connection.LocalPort is BasicsStationNetworkServer.CupsPort,
                           (ICupsProtocolMessageProcessor processor) => processor.HandleUpdateInfoAsync);

                       void Map<TService>(HttpMethod method, string pattern,
                                          Predicate<HttpContext> predicate,
                                          Func<TService, Func<HttpContext, CancellationToken, Task>> handlerMapper)
                       {
                           _ = endpoints.MapMethods(pattern, new[] { method.ToString() }, async context =>
                           {
                               if (!predicate(context))
                               {
                                   context.Response.StatusCode = (int)System.Net.HttpStatusCode.MethodNotAllowed;
                                   return;
                               }
                               var processor = context.RequestServices.GetRequiredService<TService>();
                               var handler = handlerMapper(processor);
                               await handler(context, context.RequestAborted);
                           });
                       }
                   });
        }
    }
}