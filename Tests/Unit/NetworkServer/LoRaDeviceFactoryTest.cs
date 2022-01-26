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

    public sealed class LoRaDeviceFactoryTest : IDisposable
    {
        private readonly Meter meter = new Meter(MetricRegistry.Namespace, MetricRegistry.Version);
        private readonly CancellationToken cancellationToken = CancellationToken.None;

        [Fact]
        public void Throws_When_Missing_DeviceInfo()
        {
            var factory = new TestDeviceFactory();
            var deviceInfo = DefaultDeviceInfo;
            deviceInfo.PrimaryKey = null;

            Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAndRegisterAsync(deviceInfo, CancellationToken.None));

            deviceInfo = DefaultDeviceInfo;
            deviceInfo.DevEUI = new DevEui(0);
            Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAndRegisterAsync(deviceInfo, this.cancellationToken));
        }

        [Fact]
        public void Throws_When_Device_Is_Already_Registered()
        {
            var deviceInfo = DefaultDeviceInfo;
            using var cache = CreateDefaultCache();
            var factory = new TestDeviceFactory(loRaDeviceCache: cache);

            using var device = new LoRaDevice(deviceInfo.DevAddr, deviceInfo.DevEUI, null);
            cache.Register(device);
            Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAndRegisterAsync(deviceInfo, this.cancellationToken));
        }

        [Fact]
        public async Task When_Created_Successfully_It_Is_Cached()
        {
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
            using var cache = CreateDefaultCache();
            var factory = new TestDeviceFactory(DefaultConfiguration, connectionManager.Object, cache, meter: this.meter);

            var device = await factory.CreateAndRegisterAsync(DefaultDeviceInfo, this.cancellationToken);

            Assert.True(cache.TryGetByDevEui(this.DefaultDeviceInfo.DevEUI, out var cachedDevice));
            Assert.Equal(device, cachedDevice);

            factory.LastDeviceMock.Verify(x => x.InitializeAsync(DefaultConfiguration, this.cancellationToken), Times.Once);
            connectionManager.VerifySuccess(device);
        }

        [Fact]
        public async Task When_Created_But_Not_Our_Device_It_Is_Not_Initialized_But_Connection_Is_Registered()
        {
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
            using var cache = CreateDefaultCache();
            var factory = new TestDeviceFactory(DefaultConfiguration, connectionManager.Object, cache, x => x.Object.GatewayID = "OtherGw", this.meter);

            var device = await factory.CreateAndRegisterAsync(DefaultDeviceInfo, this.cancellationToken);

            factory.LastDeviceMock.Verify(x => x.InitializeAsync(DefaultConfiguration, this.cancellationToken), Times.Never);
            connectionManager.VerifySuccess(device);
        }

        [Fact]
        public async Task When_Init_Fails_Cleaned_Up()
        {
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
            using var cache = CreateDefaultCache();
            var factory = new TestDeviceFactory(DefaultConfiguration, connectionManager.Object, cache, x => x.Setup(y => y.InitializeAsync(DefaultConfiguration, this.cancellationToken)).ReturnsAsync(false), this.meter);
            await Assert.ThrowsAsync<LoRaProcessingException>(() => factory.CreateAndRegisterAsync(DefaultDeviceInfo, this.cancellationToken));

            Assert.False(cache.TryGetByDevEui(this.DefaultDeviceInfo.DevEUI, out _));
            connectionManager.VerifyFailure(factory.LastDeviceMock.Object);
            factory.LastDeviceMock.Protected().Verify(nameof(LoRaDevice.Dispose), Times.Once(), true, true);
        }

        private readonly NetworkServerConfiguration DefaultConfiguration = new NetworkServerConfiguration { EnableGateway = true, IoTHubHostName = "TestHub", GatewayHostName = "testGw" };

        private static LoRaDeviceCache CreateDefaultCache()
            => LoRaDeviceCacheDefault.CreateDefault();

        public void Dispose()
        {
            this.meter.Dispose();
        }

        private readonly IoTHubDeviceInfo DefaultDeviceInfo = new IoTHubDeviceInfo
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
                       meter)
            {
                this.deviceSetup = deviceSetup;
            }

            internal Mock<LoRaDevice> LastDeviceMock { get; private set; }

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
        }
    }

    internal static class TestExtensions
    {
        internal static void VerifySuccess(this Mock<ILoRaDeviceClientConnectionManager> connectionManager, LoRaDevice device)
        {
            connectionManager.Verify(x => x.Register(device, It.IsAny<ILoRaDeviceClient>()), Times.Once);
            connectionManager.Verify(x => x.Release(device), Times.Never);
        }

        internal static void VerifyFailure(this Mock<ILoRaDeviceClientConnectionManager> connectionManager, LoRaDevice device)
        {
            connectionManager.Verify(x => x.Register(device, It.IsAny<ILoRaDeviceClient>()), Times.Once);
            connectionManager.Verify(x => x.Release(device), Times.Once);
        }
    }
}
