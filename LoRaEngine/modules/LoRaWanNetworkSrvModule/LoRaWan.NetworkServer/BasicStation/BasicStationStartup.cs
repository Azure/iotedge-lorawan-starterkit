// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicStation
{
    using LoRaWan.NetworkServer.BasicStation.Processors;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Globalization;

    public class BasicStationStartup
    {
        public IConfiguration Configuration { get; }
        public NetworkServerConfiguration NetworkServerConfiguration { get; }

        public BasicStationStartup(IConfiguration configuration)
        {
            this.Configuration = configuration;
            this.NetworkServerConfiguration = NetworkServerConfiguration.CreateFromEnviromentVariables();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.SetMinimumLevel((LogLevel)int.Parse(NetworkServerConfiguration.LogLevel, CultureInfo.InvariantCulture));
            });
            services.AddTransient<ILnsProcessor, LnsProcessor>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // TO DO: When certificate generation is properly handled, enable https redirection
            // app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();
            app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
