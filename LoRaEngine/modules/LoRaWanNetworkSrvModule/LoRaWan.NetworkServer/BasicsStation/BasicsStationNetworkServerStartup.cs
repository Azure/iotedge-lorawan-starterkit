// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation
{
    using System.Globalization;
    using LoRaWan.NetworkServer.BasicsStation.Processors;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    sealed class BasicsStationNetworkServerStartup
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
                        .AddTransient<ILnsProtocolMessageProcessor, LnsProtocolMessageProcessor>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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
                       _ = endpoints.MapGet("/router-info", async context =>
                           {
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDiscoveryAsync(context, context.RequestAborted);
                           });
                       _ = endpoints.MapGet("/router-data", async context =>
                           {
                               var lnsProtocolMessageProcessor = context.RequestServices.GetRequiredService<ILnsProtocolMessageProcessor>();
                               await lnsProtocolMessageProcessor.HandleDataAsync(context, context.RequestAborted);
                           });
                   });
        }
    }
}
