// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.IoTHubImpl
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools;
    using global::LoRaTools.IoTHubImpl;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class IoTHubRegistryManagerTests
    {
        private readonly MockRepository mockRepository;
        private readonly Mock<RegistryManager> mockRegistryManager;
        private readonly Mock<ILogger<IoTHubRegistryManager>> mockLogger;
        private readonly Mock<IHttpClientFactory> mockHttpClientFactory;

        public IoTHubRegistryManagerTests()
        {
            this.mockRepository = new MockRepository(MockBehavior.Strict);
            this.mockRegistryManager = this.mockRepository.Create<RegistryManager>();
            this.mockLogger = this.mockRepository.Create<ILogger<IoTHubRegistryManager>>();
            this.mockHttpClientFactory = this.mockRepository.Create<IHttpClientFactory>();
        }

        private IoTHubRegistryManager CreateManager()
        {
            this.mockRegistryManager.Protected().Setup("Dispose", ItExpr.Is<bool>(_ => true));

            return new IoTHubRegistryManager(() => this.mockRegistryManager.Object, this.mockHttpClientFactory.Object, this.mockLogger.Object);
        }

        [Fact]
        public async Task GetTwinAsync_With_CancellationToken()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var cancellationToken = CancellationToken.None;
                var twin = new Twin(deviceId);

                this.mockRegistryManager.Setup(c => c.GetTwinAsync(
                        It.Is<string>(x => x == deviceId),
                        It.Is<CancellationToken>(x => x == cancellationToken)))
                    .ReturnsAsync(twin);

                // Act
                var result = await manager.GetTwinAsync(deviceId, cancellationToken);

                // Assert
                Assert.Equal(twin, result.ToIoTHubDeviceTwin());
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task GetTwinAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var twin = new Twin(deviceId);

                this.mockRegistryManager.Setup(c => c.GetTwinAsync(
                        It.Is<string>(x => x == deviceId)))
                    .ReturnsAsync(twin);

                // Act
                var result = await manager.GetTwinAsync(deviceId);

                // Assert
                Assert.Equal(twin, result.ToIoTHubDeviceTwin());
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task GetLoRaDeviceTwinAsync_With_CancellationToken()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var cancellationToken = CancellationToken.None;
                var twin = new Twin(deviceId);

                this.mockRegistryManager.Setup(c => c.GetTwinAsync(
                        It.Is<string>(x => x == deviceId),
                        It.Is<CancellationToken>(x => x == cancellationToken)))
                    .ReturnsAsync(twin);

                // Act
                var result = await manager.GetLoRaDeviceTwinAsync(deviceId, cancellationToken);

                // Assert
                Assert.Equal(twin, result.ToIoTHubDeviceTwin());
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task GetLoRaDeviceTwinAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var twin = new Twin(deviceId);

                this.mockRegistryManager.Setup(c => c.GetTwinAsync(
                        It.Is<string>(x => x == deviceId),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(twin);

                // Act
                var result = await manager.GetLoRaDeviceTwinAsync(deviceId);

                // Assert
                Assert.Equal(twin, result.ToIoTHubDeviceTwin());
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task UpdateTwinAsync_With_Module()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var moduleId = "moduleid";
                var deviceTwin = new IoTHubDeviceTwin(new Twin());
                var eTag = "eTag";

                this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                        It.Is<string>(x => x == deviceId),
                        It.Is<string>(x => x == moduleId),
                        It.Is<Twin>(x => x == deviceTwin.TwinInstance),
                        It.Is<string>(x => x == eTag)))
                    .ReturnsAsync(deviceTwin.TwinInstance);

                // Act
                var result = await manager.UpdateTwinAsync(deviceId, moduleId, deviceTwin, eTag);

                // Assert
                Assert.Equal(deviceTwin, result);
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task UpdateTwinAsync_With_CancellationToken()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var deviceTwin = new IoTHubDeviceTwin(new Twin());
                var eTag = "eTag";
                var cancellationToken = CancellationToken.None;

                this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                        It.Is<string>(x => x == deviceId),
                        It.Is<Twin>(x => x == deviceTwin.TwinInstance),
                        It.Is<string>(x => x == eTag),
                        It.Is<CancellationToken>(x => x == cancellationToken)))
                    .ReturnsAsync(deviceTwin.TwinInstance);

                // Act
                var result = await manager.UpdateTwinAsync(deviceId, deviceTwin, eTag, cancellationToken);

                // Assert
                Assert.Equal(deviceTwin, result);
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task UpdateTwinAsync2()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var deviceTwin = new IoTHubDeviceTwin(new Twin());
                var eTag = "eTag";

                this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                        It.Is<string>(x => x == deviceId),
                        It.Is<Twin>(x => x == deviceTwin.TwinInstance),
                        It.Is<string>(x => x == eTag)))
                    .ReturnsAsync(deviceTwin.TwinInstance);

                // Act
                var result = await manager.UpdateTwinAsync(deviceId, deviceTwin, eTag);

                // Assert
                Assert.Equal(deviceTwin, result);
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task UpdateTwinAsync_With_Module_And_CancellationToken()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var moduleId = "moduleid";
                var deviceTwin = new IoTHubDeviceTwin(new Twin());
                var eTag = "eTag";
                var cancellationToken = CancellationToken.None;

                this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                        It.Is<string>(x => x == deviceId),
                        It.Is<string>(x => x == moduleId),
                        It.Is<Twin>(x => x == deviceTwin.TwinInstance),
                        It.Is<string>(x => x == eTag),
                        It.Is<CancellationToken>(x => x == cancellationToken)))
                    .ReturnsAsync(deviceTwin.TwinInstance);

                // Act
                var result = await manager.UpdateTwinAsync(
                    deviceId,
                    moduleId,
                    deviceTwin,
                    eTag,
                    cancellationToken);

                // Assert
                Assert.Equal(deviceTwin, result);
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task RemoveDeviceAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";

                this.mockRegistryManager.Setup(c => c.RemoveDeviceAsync(
                        It.Is<string>(x => x == deviceId)))
                    .Returns(Task.CompletedTask);

                // Act
                await manager.RemoveDeviceAsync(
                    deviceId);
            }

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public void GetEdgeDevices()
        {
            // Arrange
            using var manager = CreateManager();

            var mockQuery = new Mock<IQuery>();
            var twins = new List<Twin>()
                {
                    new Twin("edgeDevice") { Capabilities = new DeviceCapabilities() { IotEdge = true }},
                };

            mockQuery.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(twins);

            mockRegistryManager
                .Setup(x => x.CreateQuery(It.IsAny<string>()))
                .Returns(mockQuery.Object);

            // Act
            var result = manager.GetEdgeDevices();

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void GetAllLoRaDevices()
        {
            // Arrange
            using var manager = CreateManager();
            var mockQuery = new Mock<IQuery>();

            mockRegistryManager.Setup(c => c.CreateQuery("SELECT * FROM devices WHERE is_defined(properties.desired.AppKey) OR is_defined(properties.desired.AppSKey) OR is_defined(properties.desired.NwkSKey)"))
                .Returns(mockQuery.Object);

            // Act
            var result = manager.GetAllLoRaDevices();

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void GetLastUpdatedLoRaDevices()
        {
            // Arrange
            using var manager = CreateManager();
            var mockQuery = new Mock<IQuery>();

            var lastUpdateDateTime = DateTime.UtcNow;
            var formattedDateTime = lastUpdateDateTime.ToString(Constants.RoundTripDateTimeStringFormat, CultureInfo.InvariantCulture);

            mockRegistryManager.Setup(c => c.CreateQuery($"SELECT * FROM devices where properties.desired.$metadata.$lastUpdated >= '{formattedDateTime}' OR properties.reported.$metadata.DevAddr.$lastUpdated >= '{formattedDateTime}'"))
                .Returns(mockQuery.Object);

            // Act
            var result = manager.GetLastUpdatedLoRaDevices(lastUpdateDateTime);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void FindLoRaDeviceByDevAddr()
        {
            // Arrange
            using var manager = CreateManager();
            var someDevAddr = new DevAddr(123456789);
            var mockQuery = new Mock<IQuery>();

            mockRegistryManager.Setup(c => c.CreateQuery($"SELECT * FROM devices WHERE properties.desired.DevAddr = '{someDevAddr}' OR properties.reported.DevAddr ='{someDevAddr}'", It.IsAny<int?>()))
                .Returns(mockQuery.Object);

            // Act
            var result = manager.FindLoRaDeviceByDevAddr(someDevAddr);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void FindLnsByNetworkId()
        {
            // Arrange
            using var manager = CreateManager();
            var networkId = "aaa";
            var mockQuery = new Mock<IQuery>();

            mockRegistryManager.Setup(c => c.CreateQuery($"SELECT properties.desired.hostAddress, deviceId FROM devices.modules WHERE tags.network = '{networkId}'"))
                .Returns(mockQuery.Object);

            // Act
            var result = manager.FindLnsByNetworkId(networkId);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public void FindDeviceByDevEUI()
        {
            // Arrange
            using var manager = CreateManager();
            var devEUI = new DevEui(123456789);
            var mockQuery = new Mock<IQuery>();

            mockRegistryManager.Setup(c => c.CreateQuery($"SELECT * FROM devices WHERE deviceId = '{devEUI}'", 1))
                .Returns(mockQuery.Object);

            // Act
            var result = manager.FindDeviceByDevEUI(devEUI);

            // Assert
            Assert.NotNull(result);
        }
    }
}
