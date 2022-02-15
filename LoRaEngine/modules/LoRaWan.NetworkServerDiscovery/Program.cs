// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using LoRaWan.NetworkServerDiscovery;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddSingleton<DiscoveryService>()
                .AddSingleton(_ => new LnsDiscovery(new Uri("https://aka.ms")));

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseWebSockets();

app.MapGet(LnsDiscovery.EndpointName, (DiscoveryService discoveryService, HttpContext httpContext, CancellationToken cancellationToken) =>
    discoveryService.HandleDiscoveryRequestAsync(httpContext, cancellationToken));

app.Run();
