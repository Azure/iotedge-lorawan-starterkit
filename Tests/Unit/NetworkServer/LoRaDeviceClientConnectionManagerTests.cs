// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Linq;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class LoRaDeviceClientConnectionManagerTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(5)]
        public void When_Disposing_Should_Dispose_All_Managed_Connections(int numberOfDevices)
        {
            // arrange
            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var connectionManager = new LoRaDeviceClientConnectionManager(cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);

            var deviceRegistrations =
                Enumerable.Range(0, numberOfDevices)
                          .Select(i => TestUtils.CreateFromSimulatedDevice(new SimulatedDevice(TestDeviceInfo.CreateABPDevice((uint)i)), connectionManager))
                          .Select(d => (d, new Mock<ILoRaDeviceClient>()))
                          .ToList();

            foreach (var (d, c) in deviceRegistrations)
            {
                connectionManager.Register(d, c.Object);
            }

            // act
            connectionManager.Dispose();

            // assert
            foreach (var (_, c) in deviceRegistrations)
            {
                c.Verify(client => client.Dispose(), Times.Exactly(2));
            }
        }

        [Fact]
        public void When_Registering_Existing_Connection_Throws()
        {
            // arrange
            using var cache = new MemoryCache(new MemoryCacheOptions());
            using var connectionManager = new LoRaDeviceClientConnectionManager(cache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);

            using var loraDevice = new LoRaDevice(new DevAddr(0), "0000000000000000", null);
            connectionManager.Register(loraDevice, new Mock<ILoRaDeviceClient>().Object);
            Assert.Throws<InvalidOperationException>(() => connectionManager.Register(loraDevice, new Mock<ILoRaDeviceClient>().Object));
        }
    }
}
