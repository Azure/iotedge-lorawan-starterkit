// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using LoRaTools.NetworkServerDiscovery;
using LoRaWan;
using LoRaWan.NetworkServerDiscovery;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);
builder.Services.AddSingleton<DiscoveryService>()
                .AddSingleton<ILnsDiscovery, TagBasedLnsDiscovery>()
                .AddMemoryCache()
                .AddApplicationInsightsTelemetry()
                .AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseWebSockets();

app.MapGet(ILnsDiscovery.EndpointName, async (DiscoveryService discoveryService, HttpContext httpContext, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        await discoveryService.HandleDiscoveryRequestAsync(httpContext, cancellationToken);
    }
    catch (Exception ex) when (ExceptionFilterUtility.False(() => logger.LogError(ex, "Exception when executing discovery endpoint: '{Exception}'.", ex)))
    { }
});

app.MapMetrics();

app.Run();
