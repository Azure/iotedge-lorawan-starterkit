// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class JoinDeviceLoaderTest
    {
        [Fact]
        public async Task When_Not_In_Cache_It_Is_Loaded()
        {
            using var cache = LoRaDeviceCacheDefault.CreateDefault();
            var factory = new Mock<ILoRaDeviceFactory>();
            using var joinDeviceLoader = new JoinDeviceLoader(DefaultDeviceInfo, factory.Object, cache, NullLogger<JoinDeviceLoader>.Instance);

            await joinDeviceLoader.LoadAsync();
            factory.Verify(x => x.CreateAndRegisterAsync(DefaultDeviceInfo, It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(joinDeviceLoader.CanCache);
        }

        [Fact]
        public async Task When_In_Cache_It_Is_Not_Loaded()
        {
            using var cache = LoRaDeviceCacheDefault.CreateDefault();
            var factory = new Mock<ILoRaDeviceFactory>();
            using var joinDeviceLoader = new JoinDeviceLoader(DefaultDeviceInfo, factory.Object, cache, NullLogger<JoinDeviceLoader>.Instance);

            using var device = new LoRaDevice(DefaultDeviceInfo.DevAddr, DefaultDeviceInfo.DevEUI, null);
            cache.Register(device);

            Assert.Equal(device, await joinDeviceLoader.LoadAsync());
            factory.Verify(x => x.CreateAndRegisterAsync(DefaultDeviceInfo, It.IsAny<CancellationToken>()), Times.Never);
            Assert.True(joinDeviceLoader.CanCache);
        }

        [Fact]
        public async Task When_The_Load_Fails_CanCache_Is_False()
        {
            using var cache = LoRaDeviceCacheDefault.CreateDefault();
            var factory = new Mock<ILoRaDeviceFactory>();
            factory.Setup(x => x.CreateAndRegisterAsync(DefaultDeviceInfo, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new LoRaProcessingException());

            using var joinDeviceLoader = new JoinDeviceLoader(DefaultDeviceInfo, factory.Object, cache, NullLogger<JoinDeviceLoader>.Instance);

            Assert.Null(await joinDeviceLoader.LoadAsync());
            Assert.False(joinDeviceLoader.CanCache);
        }

        private readonly IoTHubDeviceInfo DefaultDeviceInfo = new IoTHubDeviceInfo() { DevEUI = "0000000000000000", PrimaryKey = "AAAA", DevAddr = "FFFFFFFF" };
    }
}
