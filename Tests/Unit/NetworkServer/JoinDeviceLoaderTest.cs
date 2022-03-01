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
            await using var cache = LoRaDeviceCacheDefault.CreateDefault();
            var factory = new Mock<ILoRaDeviceFactory>();
            using var joinDeviceLoader = new JoinDeviceLoader(defaultDeviceInfo, factory.Object, cache, NullLogger<JoinDeviceLoader>.Instance);

            await joinDeviceLoader.LoadAsync();
            factory.Verify(x => x.CreateAndRegisterAsync(defaultDeviceInfo, It.IsAny<CancellationToken>()), Times.Once);
            Assert.True(joinDeviceLoader.CanCache);
        }

        [Fact]
        public async Task When_In_Cache_It_Is_Not_Loaded()
        {
            await using var cache = LoRaDeviceCacheDefault.CreateDefault();
            var factory = new Mock<ILoRaDeviceFactory>();
            using var joinDeviceLoader = new JoinDeviceLoader(defaultDeviceInfo, factory.Object, cache, NullLogger<JoinDeviceLoader>.Instance);

            await using var device = new LoRaDevice(defaultDeviceInfo.DevAddr, defaultDeviceInfo.DevEUI, null);
            cache.Register(device);

            Assert.Equal(device, await joinDeviceLoader.LoadAsync());
            factory.Verify(x => x.CreateAndRegisterAsync(defaultDeviceInfo, It.IsAny<CancellationToken>()), Times.Never);
            Assert.True(joinDeviceLoader.CanCache);
        }

        [Fact]
        public async Task When_The_Load_Fails_CanCache_Is_False()
        {
            await using var cache = LoRaDeviceCacheDefault.CreateDefault();
            var factory = new Mock<ILoRaDeviceFactory>();
            factory.Setup(x => x.CreateAndRegisterAsync(defaultDeviceInfo, It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new LoRaProcessingException());

            using var joinDeviceLoader = new JoinDeviceLoader(defaultDeviceInfo, factory.Object, cache, NullLogger<JoinDeviceLoader>.Instance);

            Assert.Null(await joinDeviceLoader.LoadAsync());
            Assert.False(joinDeviceLoader.CanCache);
        }

        [Fact]
        public async Task When_One_Load_Is_Pending_Other_Is_Waiting()
        {
            await using var cache = LoRaDeviceCacheDefault.CreateDefault();
            var factory = new Mock<ILoRaDeviceFactory>();

            await using var device = new LoRaDevice(defaultDeviceInfo.DevAddr, defaultDeviceInfo.DevEUI, null);

            factory.Setup(x => x.CreateAndRegisterAsync(defaultDeviceInfo, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() =>
                    {
                        cache.Register(device);
                        return device;
                    });

            using var joinDeviceLoader = new JoinDeviceLoader(defaultDeviceInfo, factory.Object, cache, NullLogger<JoinDeviceLoader>.Instance);

            var t1 = joinDeviceLoader.LoadAsync();
            var t2 = joinDeviceLoader.LoadAsync();

            Assert.All(await Task.WhenAll(t1, t2), x => Assert.Equal(device, x));
            factory.Verify(x => x.CreateAndRegisterAsync(defaultDeviceInfo, It.IsAny<CancellationToken>()), Times.Once);
        }

        private readonly IoTHubDeviceInfo defaultDeviceInfo = new()
        {
            DevEUI = new DevEui(0),
            PrimaryKey = "AAAA",
            DevAddr = new DevAddr(0xffffffff)
        };
    }
}
