// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Threading;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class LoRaDeviceFactoryTest
    {
        private readonly CancellationToken cancellationToken = CancellationToken.None;
        [Fact]
        public void Throws_When_Missing_DeviceInfo()
        {
            var factory = CreateFactory();
            var deviceInfo = new IoTHubDeviceInfo() { DevEUI = "0000" };
            Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAndRegisterAsync(deviceInfo, CancellationToken.None));

            deviceInfo = new IoTHubDeviceInfo() { PrimaryKey = "AAAA" };
            Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAndRegisterAsync(deviceInfo, this.cancellationToken));
        }

        [Fact]
        public void Throws_When_Device_Is_Already_Registered()
        {
            var deviceInfo = CreateDummyDeviceInfo();
            using var cache = CreateDefaultCache();
            var factory = CreateFactory(loRaDeviceCache: cache);

            using var device = new LoRaDevice(deviceInfo.DevAddr, deviceInfo.DevEUI, null);
            cache.Register(device);
            Assert.ThrowsAsync<InvalidOperationException>(() => factory.CreateAndRegisterAsync(deviceInfo, this.cancellationToken));
        }

        private static LoRaDeviceCache CreateDefaultCache()
            => LoRaDeviceCacheDefault.CreateDefault();

        private static IoTHubDeviceInfo CreateDummyDeviceInfo() => new IoTHubDeviceInfo() { DevEUI = "0000000000000000", PrimaryKey = "AAAA", DevAddr = "FFFFFFFF" };

        private static LoRaDeviceFactory CreateFactory(NetworkServerConfiguration configuration = null,
                                                       ILoRaDeviceClientConnectionManager connectionManager = null,
                                                       LoRaDeviceCache loRaDeviceCache = null) =>
            new LoRaDeviceFactory(configuration ?? new NetworkServerConfiguration(),
                                 new Mock<ILoRaDataRequestHandler>().Object,
                                 connectionManager ?? new Mock<ILoRaDeviceClientConnectionManager>().Object,
                                 loRaDeviceCache,
                                 NullLoggerFactory.Instance,
                                 NullLogger<LoRaDeviceFactory>.Instance);
    }
}
