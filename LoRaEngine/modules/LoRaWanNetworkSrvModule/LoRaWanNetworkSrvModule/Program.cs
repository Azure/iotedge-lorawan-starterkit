// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace LoRaWanNetworkSrvModule
{
    using System;
    using System.Runtime.Loader;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;

    internal class Program
    {
        private static UdpServer udpServer;

        private static void Main(string[] args)
        {
            Run().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            if (udpServer != null)
            {
                udpServer.Dispose();
            }

            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the DeviceClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        private static async Task Run()
        {
            udpServer = UdpServer.Create();
            await udpServer.RunServer();
        }
    }
}
