// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.ClassB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;

#nullable enable
    internal class ClassBHost : IHostedService, IDisposable
    {
        private Timer? timer;

        public ClassBHost()
        {

        }

        public void Dispose()
        {
            timer?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // The beacon needs to be sent every 128 seconds starting from the Epoch time.
            // We first acquire a lock to start a timer on a correct 128 seconds interval.
            var epochTime = 2.0d;
            while (epochTime % 128 != 0)
            {
                epochTime = (DateTime.UtcNow -
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            }

            // Once we are in correct time we start sending beacon frames.
            timer = new Timer(SendBeacon, null, TimeSpan.Zero, TimeSpan.FromSeconds(128));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private void SendBeacon(object? state)
        {

            var message = new byte[8];
            // RFU section
            message[0] = 0x0;
            message[1] = 0x0;

            // time sectionu
            var epochTime = (long)(DateTime.UtcNow -
                    new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds % (2 ^ 32);
            BitConverter.GetBytes(epochTime).CopyTo(message, 2);

            // crc section


        }

    }
}
