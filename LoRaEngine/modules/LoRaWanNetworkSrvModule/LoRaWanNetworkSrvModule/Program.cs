// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanNetworkSrvModule
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.BasicStation;

    internal class Program
    {
        private static async Task Main()
        {
            using var cts = new CancellationTokenSource();
            var cancelKeyPresses = 0;
            Console.CancelKeyPress += (s, e) =>
            {
                if (Interlocked.Increment(ref cancelKeyPresses) == 1)
                {
                    Console.WriteLine("Stopping...");
                    try
                    {
                        cts.Cancel();
                    }
                    catch (AggregateException ex)
                    {
                        Console.Error.WriteLine(ex);
                    }
                    e.Cancel = true;
                }
            };
            
            await RunAsync(cts.Token);
        }

        /// <summary>
        /// Initializes the DeviceClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        private static async Task RunAsync(CancellationToken cancellationToken)
        {
            var configuration = NetworkServerConfiguration.CreateFromEnviromentVariables();
            using INetworkServer networkServer = configuration.UseBasicStation ? BasicStationServer.Create()
                                                                               : UdpServer.Create();
            await networkServer.RunServerAsync(cancellationToken);
        }
    }
}
