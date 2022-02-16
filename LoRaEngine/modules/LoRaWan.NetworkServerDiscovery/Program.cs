// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using LoRaWan.NetworkServerDiscovery;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddSingleton<DiscoveryService>()
                .AddSingleton<ILnsDiscovery, TagBasedLnsDiscovery>()
                .AddMemoryCache();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseWebSockets();

app.MapGet(ILnsDiscovery.EndpointName, (DiscoveryService discoveryService, HttpContext httpContext, CancellationToken cancellationToken) =>
    discoveryService.HandleDiscoveryRequestAsync(httpContext, cancellationToken));

app.Run();
