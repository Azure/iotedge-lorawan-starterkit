// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using LoRaWan.NetworkServer;
using LoRaWan.NetworkServer.BasicsStation;

// Similar to bug https://github.com/dotnet/roslyn-analyzers/issues/5184
#pragma warning disable CA1812

using var cts = new CancellationTokenSource();
var cancellationToken = cts.Token;
var cancelKeyPresses = 0;

Console.CancelKeyPress += (_, args) =>
{
    if (Interlocked.Increment(ref cancelKeyPresses) != 1)
        return;

    Console.WriteLine("Stopping...");
    try
    {
        cts.Cancel();
    }
    catch (AggregateException ex)
    {
        Console.Error.WriteLine(ex);
    }

    args.Cancel = true;
};

var configuration = NetworkServerConfiguration.CreateFromEnvironmentVariables();
var runnerTask = configuration.UseBasicsStation ? BasicsStationNetworkServer.RunServerAsync(configuration, cancellationToken)
                                                : UdpServer.RunServerAsync();
await runnerTask;
