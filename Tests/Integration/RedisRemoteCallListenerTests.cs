// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using StackExchange.Redis;
    using Xunit;
    using Xunit.Sdk;

    [Collection(RedisFixture.CollectionName)]
    public sealed class RedisRemoteCallListenerTests : IClassFixture<RedisFixture>
    {
        private readonly ConnectionMultiplexer redis;
        private readonly Mock<ILogger<RedisRemoteCallListener>> logger;
        private readonly RedisRemoteCallListener subject;

        public RedisRemoteCallListenerTests(RedisFixture redisFixture)
        {
            this.redis = redisFixture.Redis;
            this.logger = new Mock<ILogger<RedisRemoteCallListener>>();
            this.subject = new RedisRemoteCallListener(this.redis, this.logger.Object, TestMeter.Instance);
        }

        [Fact]
        public async Task Subscribe_Receives_Message()
        {
            // arrange
            var lnsName = "some-lns";
            var remoteCall = new LnsRemoteCall(RemoteCallKind.CloudToDeviceMessage, "somejsondata");
            var function = new Mock<Func<LnsRemoteCall, Task>>();

            // act
            await this.subject.SubscribeAsync(lnsName, function.Object, CancellationToken.None);
            await PublishAsync(lnsName, remoteCall);

            // assert
            await function.RetryVerifyAsync(a => a.Invoke(remoteCall), Times.Once);
        }

        [Fact]
        public async Task Subscribe_On_Different_Channel_Does_Not_Receive_Message()
        {
            // arrange
            var function = new Mock<Func<LnsRemoteCall, Task>>();

            // act
            await this.subject.SubscribeAsync("lns-1", function.Object, CancellationToken.None);
            await PublishAsync("lns-2", new LnsRemoteCall(RemoteCallKind.CloudToDeviceMessage, null));

            // assert
            await function.RetryVerifyAsync(a => a.Invoke(It.IsAny<LnsRemoteCall>()), Times.Never);
        }

        [Fact]
        public async Task UnsubscribeAsync_Unsubscribes_Successfully()
        {
            // arrange
            var lns = "lns-1";
            var function = new Mock<Func<LnsRemoteCall, Task>>();
            await this.subject.SubscribeAsync(lns, function.Object, CancellationToken.None);

            // act
            await this.subject.UnsubscribeAsync(lns, CancellationToken.None);
            await PublishAsync(lns, new LnsRemoteCall(RemoteCallKind.CloudToDeviceMessage, null));

            // assert
            function.Verify(a => a.Invoke(It.IsAny<LnsRemoteCall>()), Times.Never);
        }

        [Fact]
        public async Task SubscribeAsync_Exceptions_Are_Tracked()
        {
            // arrange
            var lns = "lns-1";
            var function = new Mock<Func<LnsRemoteCall, Task>>();

            // act
            await this.subject.SubscribeAsync(lns, function.Object, CancellationToken.None);
            await this.redis.GetSubscriber().PublishAsync(lns, string.Empty);

            // assert
            var invocation = await RetryAssertSingleAsync(this.logger.GetLogInvocations());
            _ = Assert.IsType<ArgumentNullException>(invocation.Exception);
        }

        private async Task PublishAsync(string channel, LnsRemoteCall lnsRemoteCall)
        {
            await this.redis.GetSubscriber().PublishAsync(channel, JsonSerializer.Serialize(lnsRemoteCall));
        }

        private static async Task<T> RetryAssertSingleAsync<T>(IEnumerable<T> sequence,
                                                               int numberOfRetries = 5,
                                                               TimeSpan? delay = null)
        {
            var retryDelay = delay ?? TimeSpan.FromMilliseconds(50);
            for (var i = 0; i < numberOfRetries + 1; ++i)
            {
                try
                {
                    var result = Assert.Single(sequence);
                    return result;
                }
                catch (SingleException) when (i < numberOfRetries)
                {
                    // assertion does not yet pass, retry once more.
                    await Task.Delay(retryDelay);
                    continue;
                }
            }

            throw new InvalidOperationException("asdfasdf");
        }
    }
}
