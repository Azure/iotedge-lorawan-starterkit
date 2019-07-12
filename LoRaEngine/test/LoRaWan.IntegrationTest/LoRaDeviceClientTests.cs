// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Xunit;
    using XunitRetryHelper;

    /// <summary>
    /// Integration tests for <see cref="LoRaDeviceClient"/>
    /// </summary>
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public class LoRaDeviceClientTests : IClassFixture<IntegrationTestFixtureCi>
    {
        private readonly IntegrationTestFixtureCi fixture;

        class LoRaDeviceFactoryForTest : ILoRaDeviceFactory
        {
            public LoRaDevice Create(IoTHubDeviceInfo deviceInfo)
            {
                throw new NotImplementedException();
            }

            public IIoTHubDeviceClient CreateDeviceClient(string connectionString)
            {
                // Enabling AMQP multiplexing
                var transportSettings = new ITransportSettings[]
                {
                    new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                    {
                        AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                        {
                            Pooling = true,
                            MaxPoolSize = 1
                        }
                    }
                };

                return new IoTHubDeviceClient(DeviceClient.CreateFromConnectionString(connectionString, transportSettings));
            }
        }

        public LoRaDeviceClientTests(IntegrationTestFixtureCi fixture)
        {
            this.fixture = fixture;
        }

        static async Task<(LoRaDeviceClient client, Message message)> ReceiveAsync(LoRaDeviceClient client, int timeout)
        {
            var msg = await client.ReceiveAsync(TimeSpan.FromMilliseconds(timeout));
            return (client, msg);
        }

        [Theory(Skip = "Running from same machine fails in CompleteAsync with a object null reference exception")]
        [InlineData(1)]
        [InlineData(2)]
        public async Task When_Receiving_Message_With_Multiple_Device_Clients_Should_Receive_Only_Once(int deviceClientCount)
        {
            const int cleanUpReceiveAsyncTimeout = 1000;
            const int receiveAsyncTimeout = 500;
            const int cleanUpLoops = 10;
            const int maxReceiveLoops = 5;
            const int msgsToSend = 5;
            const int delayInBetweenReceiveAsync = 1000;
            const int delayInBetweenCleanupLoops = 400;
            const int delayInBetweenMessages = 1000;

            var device = this.fixture.Device31_ABP;

            var deviceConnectionString = this.fixture.GetDeviceClientConnectionString(device.DeviceID);
            var deviceClients = new LoRaDeviceClient[deviceClientCount];
            var loRaDeviceFactory = new LoRaDeviceFactoryForTest();
            for (var deviceIndex = 0; deviceIndex < deviceClientCount; ++deviceIndex)
            {
                deviceClients[deviceIndex] = new LoRaDeviceClient(device.DeviceID, deviceConnectionString, loRaDeviceFactory);
            }

            // clean up any pending message
            for (var i = 0; i < cleanUpLoops; ++i)
            {
                var tasks = new List<Task<(LoRaDeviceClient client, Message message)>>(deviceClientCount);
                for (var deviceIndex = 0; deviceIndex < deviceClientCount; ++deviceIndex)
                {
                    tasks.Add(ReceiveAsync(deviceClients[deviceIndex], cleanUpReceiveAsyncTimeout));
                }

                var msgsToCleanUp = await Task.WhenAll(tasks);

                foreach (var (client, message) in msgsToCleanUp.Where(x => x.message != null))
                {
                    await client.CompleteAsync(message);
                }

                await Task.Delay(delayInBetweenCleanupLoops);
            }

            for (var currentMessage = 1; currentMessage <= msgsToSend; currentMessage++)
            {
                // send message 1
                var msgCorrelationId = Guid.NewGuid().ToString();
                var messageBody = Guid.NewGuid().ToByteArray();
                var message = new Microsoft.Azure.Devices.Message(messageBody)
                {
                    MessageId = msgCorrelationId,
                    CorrelationId = msgCorrelationId,
                };

                await this.fixture.SendCloudToDeviceMessageAsync(device.DeviceID, message);

                var receiveLoops = 0;
                for (; receiveLoops < maxReceiveLoops; ++receiveLoops)
                {
                    var tasks = new List<Task<(LoRaDeviceClient client, Message message)>>(deviceClientCount);
                    for (var deviceIndex = 0; deviceIndex < deviceClientCount; ++deviceIndex)
                    {
                        tasks.Add(ReceiveAsync(deviceClients[deviceIndex], receiveAsyncTimeout));
                    }

                    var returnedMessages = await Task.WhenAll(tasks);

                    var receivedMessagesCount = returnedMessages.Count(x => x.message != null && x.message.CorrelationId == msgCorrelationId);
                    Assert.True(receivedMessagesCount <= 1, $"Message #{currentMessage} was received more than once: {receivedMessagesCount} times");

                    if (receivedMessagesCount > 0)
                    {
                        var receivedMsg = returnedMessages.First(x => x.message != null && x.message.CorrelationId == msgCorrelationId);
                        if (!await receivedMsg.client.CompleteAsync(receivedMsg.message))
                        {
                            await Task.Delay(10);
                            await receivedMsg.client.CompleteAsync(receivedMsg.message);
                        }

                        break;
                    }

                    // reject messages that are not from this correlation_id
                    foreach (var outOfScopeMsg in returnedMessages.Where(x => x.message != null && x.message.CorrelationId != msgCorrelationId))
                    {
                        if (!await outOfScopeMsg.client.CompleteAsync(outOfScopeMsg.message))
                        {
                            await Task.Delay(10);
                            await outOfScopeMsg.client.CompleteAsync(outOfScopeMsg.message);
                        }
                    }

                    await Task.Delay(delayInBetweenReceiveAsync);
                }

                Assert.False(receiveLoops == maxReceiveLoops, $"Should have received the message, currently at message {currentMessage}");

                await Task.Delay(delayInBetweenMessages);
            }
        }

        [RetryFact]
        public async Task When_Receiving_Message_With_Single_Device_Client_Not_Racing_Should_Receive_Only_Once()
        {
            await this.When_Receiving_Message_Not_Racing_Should_Receive_Only_Once(1, this.fixture.Device31_ABP);
        }

        [RetryFact]
        public async Task When_Receiving_Message_With_Two_Device_Client_Not_Racing_Should_Receive_Only_Once()
        {
            await this.When_Receiving_Message_Not_Racing_Should_Receive_Only_Once(2, this.fixture.Device32_ABP);
        }

        async Task When_Receiving_Message_Not_Racing_Should_Receive_Only_Once(
            int deviceClientCount,
            TestDeviceInfo device)
        {
            const int cleanUpReceiveAsyncTimeout = 1000;
            const int receiveAsyncTimeout = 500;
            const int cleanUpLoops = 10;
            const int maxReceiveLoops = 5;
            const int msgsToSend = 5;
            const int delayInBetweenReceiveAsync = 1000;
            const int delayInBetweenCleanupLoops = 400;
            const int delayInBetweenMessages = 1000;

            var deviceConnectionString = this.fixture.GetDeviceClientConnectionString(device.DeviceID);
            var deviceClients = new LoRaDeviceClient[deviceClientCount];
            var loRaDeviceFactory = new LoRaDeviceFactoryForTest();
            for (var deviceIndex = 0; deviceIndex < deviceClientCount; ++deviceIndex)
            {
                deviceClients[deviceIndex] = new LoRaDeviceClient(device.DeviceID, deviceConnectionString, loRaDeviceFactory);
            }

            // clean up any pending message
            for (var i = 0; i < cleanUpLoops; ++i)
            {
                for (var deviceIndex = 0; deviceIndex < deviceClientCount; ++deviceIndex)
                {
                    var msg = await deviceClients[deviceIndex].ReceiveAsync(TimeSpan.FromMilliseconds(cleanUpReceiveAsyncTimeout));
                    if (msg != null)
                    {
                        await deviceClients[deviceIndex].CompleteAsync(msg);
                    }
                }

                await Task.Delay(delayInBetweenCleanupLoops);
            }

            var firstDeviceIndex = 0;

            for (var currentMessage = 1; currentMessage <= msgsToSend; currentMessage++)
            {
                var msgCorrelationId = Guid.NewGuid().ToString();
                var messageBody = Guid.NewGuid().ToByteArray();
                var message = new Microsoft.Azure.Devices.Message(messageBody)
                {
                    MessageId = msgCorrelationId,
                    CorrelationId = msgCorrelationId,
                };

                await this.fixture.SendCloudToDeviceMessageAsync(device.DeviceID, message);

                var receiveLoops = 0;

                var devicesInOrder = new List<LoRaDeviceClient>();
                devicesInOrder.AddRange(deviceClients.Skip(firstDeviceIndex));
                if (firstDeviceIndex > 0)
                {
                    devicesInOrder.AddRange(deviceClients.Take(firstDeviceIndex));
                }

                firstDeviceIndex++;
                if (firstDeviceIndex > deviceClients.Length)
                    firstDeviceIndex = 0;

                var msgReceivedCount = 0;

                for (; receiveLoops < maxReceiveLoops; ++receiveLoops)
                {
                    foreach (var deviceClient in devicesInOrder)
                    {
                        var msg = await deviceClient.ReceiveAsync(TimeSpan.FromMilliseconds(receiveAsyncTimeout));
                        if (msg != null)
                        {
                            // no wait so we can try to get the message in other device quicker
                            _ = deviceClient.CompleteAsync(msg);

                            Assert.Equal(msg.CorrelationId, msgCorrelationId);
                            msgReceivedCount++;
                        }
                    }

                    Assert.False(msgReceivedCount > 1, $"The message {msgCorrelationId} was received {msgReceivedCount} times");

                    if (msgReceivedCount > 0)
                        break;

                    await Task.Delay(delayInBetweenReceiveAsync);
                }

                Assert.False(receiveLoops == maxReceiveLoops, $"Should have received the message, currently at message {currentMessage}");

                await Task.Delay(delayInBetweenMessages);
            }
        }
    }
}
