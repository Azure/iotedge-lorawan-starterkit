// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;

    public class LoRaDeviceClientTest
    {
        private readonly Mock<IIoTHubDeviceClient> deviceClient;

        public LoRaDeviceClientTest()
        {
            this.deviceClient = new Mock<IIoTHubDeviceClient>();
        }

        [Theory]
        [InlineData(9, 500, 1)] // ~9 attempts per resolution
        [InlineData(60, 500, 15)] // ~4 attempts per resolution
        [InlineData(1000, 500, 200)] // ~5 attempts per resolution
        [InlineData(600, 500, 100)] // ~6 attempts per resolution
        public async Task When_ReceiveAsync_Is_Resolved_Should_Return_Message_To_Single_Caller(
            int responseDelay,
            int timeout,
            int delayBetweenTaskStarts)
        {
            var msg1 = new Message();
            var msg2 = new Message();
            var msg3 = new Message();
            var responses = new ConcurrentQueue<Message>(new[] { msg1, msg2, msg3 });

            this.deviceClient.Setup(x => x.ReceiveAsync(It.IsNotNull<TimeSpan>()))
                            .Returns(() =>
                            {
                                return Task.Delay(responseDelay).ContinueWith((t) =>
                                {
                                    return responses.TryDequeue(out var res) ? res : null;
                                });
                            });

            var factory = new Mock<ILoRaDeviceFactory>();
            factory.Setup(x => x.CreateDeviceClient(It.IsNotNull<string>()))
                .Returns(this.deviceClient.Object);

            var target = new LoRaDeviceClient(
                "0123456789",
                "connectionString",
                factory.Object);

            var receivedMessages = new ConcurrentQueue<Message>();

            async Task Receiver()
            {
                var msg = await target.ReceiveAsync(TimeSpan.FromMilliseconds(timeout));
                if (msg != null)
                {
                    receivedMessages.Enqueue(msg);
                }
            }

            var runners = new List<Task>();
            for (var i = 0; i < 30; ++i)
            {
                runners.Add(Task.Run(async () => await Receiver()));
                if (delayBetweenTaskStarts > 0)
                {
                    await Task.Delay(delayBetweenTaskStarts);
                }
            }

            await Task.WhenAll(runners);

            Assert.Equal(3, receivedMessages.Count);
            Assert.Empty(responses);

            // each message is seen only once
            var receivedMessagesArray = receivedMessages.ToArray();
            Assert.Single(receivedMessagesArray.Where(x => x == msg1));
            Assert.Single(receivedMessagesArray.Where(x => x == msg2));
            Assert.Single(receivedMessagesArray.Where(x => x == msg3));
        }
    }
}
