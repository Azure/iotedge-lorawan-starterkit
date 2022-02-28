// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public sealed class LoRaDeviceFactoryTest
    {
        private readonly CancellationToken cancellationToken = CancellationToken.None;

        [Fact]
        public void Throws_When_Missing_DeviceInfo()
        {
            var factory = new TestDeviceFactory();
            var deviceInfo = defaultDeviceInfo;
            deviceInfo.PrimaryKey = null;

            Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAndRegisterAsync(deviceInfo, CancellationToken.None));

            deviceInfo = defaultDeviceInfo;
            deviceInfo.DevEUI = new DevEui(0);
            Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAndRegisterAsync(deviceInfo, this.cancellationToken));
        }

        [Fact]
        public async Task Throws_When_Device_Is_Already_Registered()
        {
            var deviceInfo = defaultDeviceInfo;
            await using var cache = CreateDefaultCache();
            var factory = new TestDeviceFactory(loRaDeviceCache: cache);

            await using var device = new LoRaDevice(deviceInfo.DevAddr, deviceInfo.DevEUI, null);
            cache.Register(device);
            await Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAndRegisterAsync(deviceInfo, this.cancellationToken));
        }

        [Fact]
        public async Task When_Created_Successfully_It_Is_Cached()
        {
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
            await using var cache = CreateDefaultCache();
            var factory = new TestDeviceFactory(defaultConfiguration, connectionManager.Object, cache, meter: TestMeter.Instance);

            var device = await factory.CreateAndRegisterAsync(defaultDeviceInfo, this.cancellationToken);

            Assert.True(cache.TryGetByDevEui(this.defaultDeviceInfo.DevEUI, out var cachedDevice));
            Assert.Equal(device, cachedDevice);

            factory.LastDeviceMock.Verify(x => x.InitializeAsync(defaultConfiguration, this.cancellationToken), Times.Once);
            connectionManager.VerifySuccess(device);
            factory.LastDeviceClientMock.Verify(x => x.DisposeAsync(), Times.Never());
        }

        [Fact]
        public async Task When_Created_But_Not_Our_Device_It_Is_Not_Initialized_But_Connection_Is_Registered()
        {
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
            await using var cache = CreateDefaultCache();
            var factory = new TestDeviceFactory(defaultConfiguration, connectionManager.Object, cache, x => x.Object.GatewayID = "OtherGw", TestMeter.Instance);

            var device = await factory.CreateAndRegisterAsync(defaultDeviceInfo, this.cancellationToken);

            factory.LastDeviceMock.Verify(x => x.InitializeAsync(defaultConfiguration, this.cancellationToken), Times.Never);
            connectionManager.VerifySuccess(device);
            factory.LastDeviceClientMock.Verify(x => x.DisposeAsync(), Times.Never());
        }

        [Fact]
        public async Task When_Init_Fails_Cleaned_Up()
        {
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
            await using var cache = CreateDefaultCache();
            var factory = new TestDeviceFactory(defaultConfiguration, connectionManager.Object, cache, x => x.Setup(y => y.InitializeAsync(defaultConfiguration, this.cancellationToken)).ReturnsAsync(false), TestMeter.Instance);
            await Assert.ThrowsAsync<LoRaProcessingException>(() => factory.CreateAndRegisterAsync(defaultDeviceInfo, this.cancellationToken));

            Assert.False(cache.TryGetByDevEui(this.defaultDeviceInfo.DevEUI, out _));
            factory.LastDeviceClientMock.Verify(x => x.DisposeAsync(), Times.Once());
            factory.LastDeviceMock.Protected().Verify("DisposeAsyncCore", Times.Once());
        }

        private readonly NetworkServerConfiguration defaultConfiguration = new NetworkServerConfiguration { EnableGateway = true, IoTHubHostName = "TestHub", GatewayHostName = "testGw" };

        private static LoRaDeviceCache CreateDefaultCache()
            => LoRaDeviceCacheDefault.CreateDefault();

        private readonly IoTHubDeviceInfo defaultDeviceInfo = new IoTHubDeviceInfo
        {
            DevEUI = new DevEui(1),
            PrimaryKey = "AAAA",
            DevAddr = new DevAddr(0xffffffff),
        };

        private class TestDeviceFactory : LoRaDeviceFactory
        {
            private readonly Action<Mock<LoRaDevice>> deviceSetup;

            public TestDeviceFactory(NetworkServerConfiguration configuration = null,
                                     ILoRaDeviceClientConnectionManager connectionManager = null,
                                     LoRaDeviceCache loRaDeviceCache = null,
                                     Action<Mock<LoRaDevice>> deviceSetup = null,
                                     Meter meter = null)
                : base(configuration ?? new NetworkServerConfiguration(),
                       new Mock<ILoRaDataRequestHandler>().Object,
                       connectionManager ?? new Mock<ILoRaDeviceClientConnectionManager>().Object,
                       loRaDeviceCache,
                       NullLoggerFactory.Instance,
                       NullLogger<LoRaDeviceFactory>.Instance,
                       meter,
                       new NoopTracing())
            {
                this.deviceSetup = deviceSetup;
            }

            internal Mock<LoRaDevice> LastDeviceMock { get; private set; }

            internal Mock<ILoRaDeviceClient> LastDeviceClientMock { get; private set; }

            protected override LoRaDevice CreateDevice(IoTHubDeviceInfo deviceInfo)
            {
                var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
                var device = new Mock<LoRaDevice>(deviceInfo.DevAddr, deviceInfo.DevEUI, connectionManager.Object);
                if (this.deviceSetup != null)
                {
                    this.deviceSetup(device);
                }
                else
                {
                    device.Setup(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
                }
                LastDeviceMock = device;
                return device.Object;
            }

            public override ILoRaDeviceClient CreateDeviceClient(string deviceId, string primaryKey)
            {
                LastDeviceClientMock = new Mock<ILoRaDeviceClient>();
                return LastDeviceClientMock.Object;
            }
        }
    }

    internal static class TestExtensions
    {
        internal static void VerifySuccess(this Mock<ILoRaDeviceClientConnectionManager> connectionManager, LoRaDevice device)
        {
            connectionManager.Verify(x => x.Register(device, It.IsAny<ILoRaDeviceClient>()), Times.Once);
            connectionManager.Verify(x => x.ReleaseAsync(device), Times.Never);
        }

        internal static void VerifyFailure(this Mock<ILoRaDeviceClientConnectionManager> connectionManager, LoRaDevice device)
        {
            connectionManager.Verify(x => x.Register(device, It.IsAny<ILoRaDeviceClient>()), Times.Once);
            connectionManager.Verify(x => x.ReleaseAsync(device), Times.Once);
        }
    }
}
