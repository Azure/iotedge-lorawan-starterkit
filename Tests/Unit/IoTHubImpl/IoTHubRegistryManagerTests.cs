// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.IoTHubImpl
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net;
    using System.Net.Http;
    using System.Net.Mime;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools;
    using global::LoRaTools.IoTHubImpl;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Moq.Protected;
    using Newtonsoft.Json;
    using RichardSzalay.MockHttp;
    using Xunit;
    using Xunit.Abstractions;

    public class IoTHubRegistryManagerTests : IAsyncDisposable
    {
        private readonly MockRepository mockRepository;
        private readonly Mock<RegistryManager> mockRegistryManager;
        private readonly Mock<IHttpClientFactory> mockHttpClientFactory;

#pragma warning disable CA2213 // Disposable fields should be disposed (false positive)
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;
        private readonly MockHttpMessageHandler mockHttpClientHandler;
#pragma warning restore CA2213 // Disposable fields should be disposed
        private bool disposedValue;

        public IoTHubRegistryManagerTests(ITestOutputHelper testOutputHelper)
        {
            this.mockRepository = new MockRepository(MockBehavior.Strict);
            this.mockRegistryManager = this.mockRepository.Create<RegistryManager>();
            this.mockHttpClientFactory = this.mockRepository.Create<IHttpClientFactory>();

            this.mockHttpClientHandler = new MockHttpMessageHandler();

            this.testOutputLoggerFactory = new TestOutputLoggerFactory(testOutputHelper);
        }

        private IoTHubRegistryManager CreateManager()
        {
            this.mockRegistryManager.Protected().Setup("Dispose", ItExpr.Is<bool>(_ => true));

            return new IoTHubRegistryManager(() => this.mockRegistryManager.Object, mockHttpClientFactory.Object, testOutputLoggerFactory.CreateLogger(nameof(RegistryManager)));
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
                        It.Is<string>(x => x == deviceId),
                        It.IsAny<CancellationToken>()))
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
        public async Task GetStationTwinAsync()
        {
            var stationEui = new StationEui(01234);

            // Arrange
            using (var manager = CreateManager())
            {
                var twin = new Twin(stationEui.ToString());

                this.mockRegistryManager.Setup(c => c.GetTwinAsync(
                        It.Is<string>(x => x == stationEui.ToString()),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(twin);

                // Act
                var result = await manager.GetStationTwinAsync(stationEui);

                // Assert
                Assert.Equal(twin, result.ToIoTHubDeviceTwin());
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

        [Fact]
        public async Task GetDevicePrimaryKeyAsync()
        {
            // Arrange
            using var manager = CreateManager();
            var devEUI = new DevEui(123456789);

            const string mockPrimaryKey = "YWFhYWFhYWFhYWFhYWFhYQ==";

            mockRegistryManager.Setup(c => c.GetDeviceAsync(It.Is(devEUI.ToString(), StringComparer.OrdinalIgnoreCase)))
                .ReturnsAsync((string deviceId) => new Device(deviceId) { Authentication = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = mockPrimaryKey } } });

            // Act
            var result = await manager.GetDevicePrimaryKeyAsync(devEUI.ToString());

            // Assert
            Assert.Equal(mockPrimaryKey, result);
        }

        protected virtual ValueTask DisposeAsync(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    this.testOutputLoggerFactory.Dispose();
                    this.mockHttpClientHandler.Dispose();
                }

                this.disposedValue = true;
            }

            return new ValueTask();
        }

        public async ValueTask DisposeAsync()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            await DisposeAsync(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
