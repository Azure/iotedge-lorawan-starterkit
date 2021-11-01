// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System;
    using System.Globalization;
    using System.Net.WebSockets;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
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
                        .AddTransient<ILnsProtocolMessageProcessor, LnsProtocolMessageProcessor>()
                        .AddSingleton<IBasicsStationConfigurationService, BasicsStationConfigurationService>();
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
                       _ = endpoints.MapGet($"{BasicsStationNetworkServer.DataEndpoint}/{{{BasicsStationNetworkServer.RouterIdPathParameterName}:required}}", async context =>
                           {
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDataAsync(context, context.RequestAborted);
                           });
                       _ = endpoints.MapGet("/test/{id:required}", Test);
                   });
        }

        private static Task Test(HttpContext context) =>
            Test(context, context.RequestAborted);

        private static readonly WebSocketWriterRegistry<string, string> WebSocketWriterRegistry = new(null);

        private static async Task Test(HttpContext context, CancellationToken cancellationToken)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var socket = await context.WebSockets.AcceptWebSocketAsync();
            var id = (string)context.Request.RouteValues["id"];
            var channel = new WebSocketTextChannel(socket, sendTimeout: TimeSpan.FromSeconds(3));
            var h = WebSocketWriterRegistry.Register(id, channel);
            _ = Task.Run(cancellationToken: cancellationToken, function: async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                    try
                    {
                        await h.SendAsync(DateTime.Now.ToLongTimeString(), cancellationToken);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e);
                    }
                }
            });

            using var cts1 = new CancellationTokenSource();
            using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cts1.Token, cancellationToken);
            var task = channel.ProcessSendQueueAsync(cts2.Token);
            await using var message = socket.ReadTextMessages(cancellationToken);
            while (await message.MoveNextAsync())
                await channel.SendAsync("< " + message.Current, cancellationToken);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Goodbye", cancellationToken);
            _ = WebSocketWriterRegistry.Deregister(id);
            cts1.Cancel();
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                // ignore because it is expected
            }
        }
    }
}
