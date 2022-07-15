// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.IoTHubImpl
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.IoTHubImpl;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class IoTHubRegistryManagerTests
    {
        private readonly MockRepository mockRepository;
        private readonly Mock<RegistryManager> mockRegistryManager;

        public IoTHubRegistryManagerTests()
        {
            this.mockRepository = new MockRepository(MockBehavior.Strict);
            this.mockRegistryManager = this.mockRepository.Create<RegistryManager>();
        }

        private IoTHubRegistryManager CreateManager()
        {
            this.mockRegistryManager.Protected().Setup("Dispose", ItExpr.Is<bool>(_ => true));

            return new IoTHubRegistryManager(() => this.mockRegistryManager.Object);
        }

        [Fact]
        public async Task AddConfigurationAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var configuration = new Configuration("testConfiguration");

                this.mockRegistryManager.Setup(c => c.AddConfigurationAsync(It.Is<Configuration>(x => x == configuration)))
                    .ReturnsAsync(configuration);

                // Act
                var result = await manager.AddConfigurationAsync(configuration);

                // Assert
                Assert.Equal(configuration, result);
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task AddDeviceAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var edgeGatewayDevice = new Device("deviceid");

                this.mockRegistryManager.Setup(c => c.AddDeviceAsync(It.Is<Device>(x => x == edgeGatewayDevice)))
                    .ReturnsAsync(edgeGatewayDevice);

                // Act
                var result = await manager.AddDeviceAsync(edgeGatewayDevice);

                // Assert
                Assert.Equal(edgeGatewayDevice, result);
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task AddDeviceWithTwinAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var device = new Device("deviceid");
                var twin = new IoTHubDeviceTwin(new Twin("deviceid"));

                this.mockRegistryManager.Setup(c => c.AddDeviceWithTwinAsync(
                        It.Is<Device>(x => x == device),
                        It.Is<Twin>(x => x == twin.TwinInstance)))
                    .ReturnsAsync(new BulkRegistryOperationResult
                    {
                        IsSuccessful = true
                    });

                // Act
                var result = await manager.AddDeviceWithTwinAsync(device, twin);

                // Assert
                Assert.NotNull(result);
                Assert.True(result.IsSuccessful);
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task AddModuleAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var moduleToAdd = new Module();

                this.mockRegistryManager.Setup(c => c.AddModuleAsync(
                        It.Is<Module>(x => x == moduleToAdd)))
                    .ReturnsAsync(moduleToAdd);

                // Act
                var result = await manager.AddModuleAsync(moduleToAdd);

                // Assert
                Assert.Equal(moduleToAdd, result);
            }

            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task ApplyConfigurationContentOnDeviceAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceName = "deviceid";
                var deviceConfigurationContent = new ConfigurationContent();

                this.mockRegistryManager.Setup(c => c.ApplyConfigurationContentOnDeviceAsync(
                        It.Is<string>(x => x == deviceName),
                        It.Is<ConfigurationContent>(x => x == deviceConfigurationContent)))
                    .Returns(Task.CompletedTask);

                // Act
                await manager.ApplyConfigurationContentOnDeviceAsync(deviceName, deviceConfigurationContent);
            }

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task GetDeviceAsync()
        {
            // Arrange
            using (var manager = CreateManager())
            {
                var deviceId = "deviceid";
                var device = new Device(deviceId);

                this.mockRegistryManager.Setup(c => c.GetDeviceAsync(
                        It.Is<string>(x => x == deviceId)))
                    .ReturnsAsync(device);

                // Act
                var result = await manager.GetDeviceAsync(deviceId);

                // Assert
                Assert.Equal(device, result);
            }

            this.mockRepository.VerifyAll();
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
        public async Task GetEdgeDevices()
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
            var result = await manager.GetEdgeDevicesAsync(CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.Equal("edgeDevice", result.First().DeviceId);
        }

        [Fact]
        public async Task GetEdgeDevicesShouldThrowsAnExceptionIfCanceled()
        {
            // Arrange
            using var manager = CreateManager();
            using var cancelationTokenSource = new CancellationTokenSource();

            var mockQuery = new Mock<IQuery>();
            var twins = new List<Twin>()
                {
                    new Twin("edgeDevice") { Capabilities = new DeviceCapabilities() { IotEdge = true }},
                };

            cancelationTokenSource.Cancel(true);

            mockQuery.Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(twins);

            mockRegistryManager
                .Setup(x => x.CreateQuery(It.IsAny<string>()))
                .Returns(mockQuery.Object);

            // Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => manager.GetEdgeDevicesAsync(cancelationTokenSource.Token));
        }
    }
}
