// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using Moq;
    using StackExchange.Redis;
    using Xunit;

    [Collection(RedisFixture.CollectionName)]
    public sealed class RedisRemoteCallListenerTests : IClassFixture<RedisFixture>
    {
        private readonly ConnectionMultiplexer redis;
        private readonly RedisRemoteCallListener subject;

        public RedisRemoteCallListenerTests(RedisFixture redisFixture)
        {
            this.redis = redisFixture.Redis;
            this.subject = new RedisRemoteCallListener(this.redis);
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
            function.Verify(a => a.Invoke(remoteCall), Times.Once);
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
            function.Verify(a => a.Invoke(It.IsAny<LnsRemoteCall>()), Times.Never);
        }

        private async Task PublishAsync(string channel, LnsRemoteCall lnsRemoteCall)
        {
            await this.redis.GetSubscriber().PublishAsync(channel, JsonSerializer.Serialize(lnsRemoteCall));
        }
    }
}
