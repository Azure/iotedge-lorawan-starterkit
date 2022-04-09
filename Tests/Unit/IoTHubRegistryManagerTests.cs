// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
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

        private IDeviceRegistryManager CreateManager()
        {
            return IoTHubRegistryManager.CreateWithProvider(() => this.mockRegistryManager.Object);
        }

        [Fact]
        public async Task AddConfigurationAsync()
        {
            // Arrange
            var manager = this.CreateManager();
            var configuration = new Configuration("testConfiguration");

            this.mockRegistryManager.Setup(c => c.AddConfigurationAsync(It.Is<Configuration>(x => x == configuration)))
                .ReturnsAsync(configuration);

            // Act
            var result = await manager.AddConfigurationAsync(configuration);

            // Assert
            Assert.Equal(configuration, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task AddDeviceAsync()
        {
            // Arrange
            var manager = this.CreateManager();
            var edgeGatewayDevice = new Device("deviceid");

            this.mockRegistryManager.Setup(c => c.AddDeviceAsync(It.Is<Device>(x => x == edgeGatewayDevice)))
                    .ReturnsAsync(edgeGatewayDevice);

            // Act
            var result = await manager.AddDeviceAsync(edgeGatewayDevice);

            // Assert
            Assert.Equal(edgeGatewayDevice, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task AddDeviceWithTwinAsync()
        {
            // Arrange
            var manager = this.CreateManager();
            var device = new Device("deviceid");
            var twin = new Twin("deviceid");

            this.mockRegistryManager.Setup(c => c.AddDeviceWithTwinAsync(
                    It.Is<Device>(x => x == device),
                    It.Is<Twin>(x => x == twin)))
                    .ReturnsAsync(new BulkRegistryOperationResult
                    {
                        IsSuccessful = true
                    });

            // Act
            var result = await manager.AddDeviceWithTwinAsync(device, twin);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsSuccessful);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task AddModuleAsync()
        {
            // Arrange
            var manager = this.CreateManager();
            var moduleToAdd = new Module();

            this.mockRegistryManager.Setup(c => c.AddModuleAsync(
                It.Is<Module>(x => x == moduleToAdd)))
                .ReturnsAsync(moduleToAdd);

            // Act
            var result = await manager.AddModuleAsync(moduleToAdd);

            // Assert
            Assert.Equal(moduleToAdd, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task ApplyConfigurationContentOnDeviceAsync()
        {
            // Arrange
            var manager = this.CreateManager();
            var deviceName = "deviceid";
            var deviceConfigurationContent = new ConfigurationContent();

            this.mockRegistryManager.Setup(c => c.ApplyConfigurationContentOnDeviceAsync(
                It.Is<string>(x => x == deviceName),
                It.Is<ConfigurationContent>(x => x == deviceConfigurationContent)))
                .Returns(Task.CompletedTask);

            // Act
            await manager.ApplyConfigurationContentOnDeviceAsync(deviceName, deviceConfigurationContent);

            // Assert
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public void CreateQuery()
        {
            // Arrange
            var manager = this.CreateManager();
            var query = "new query";
            var mockQuery = this.mockRepository.Create<IQuery>();

            this.mockRegistryManager.Setup(c => c.CreateQuery(
                It.Is<string>(x => x == query)))
                .Returns(mockQuery.Object);

            // Act
            var result = manager.CreateQuery(query);

            // Assert
            Assert.Equal(mockQuery.Object, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public void CreateQuery_WithPageSize()
        {
            // Arrange
            var manager = this.CreateManager();
            var pageSize = 10;
            var query = "new query";
            var mockQuery = this.mockRepository.Create<IQuery>();

            this.mockRegistryManager.Setup(c => c.CreateQuery(
                It.Is<string>(x => x == query),
                It.Is<int>(x => x == pageSize)))
                .Returns(mockQuery.Object);

            // Act
            var result = manager.CreateQuery(query, pageSize);

            // Assert
            Assert.Equal(mockQuery.Object, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task GetDeviceAsync()
        {
            // Arrange
            var manager = this.CreateManager();
            var deviceId = "deviceid";
            var device = new Device(deviceId);

            this.mockRegistryManager.Setup(c => c.GetDeviceAsync(
                It.Is<string>(x => x == deviceId)))
                .ReturnsAsync(device);

            // Act
            var result = await manager.GetDeviceAsync(deviceId);

            // Assert
            Assert.Equal(device, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task GetTwinAsync_With_CancellationToken()
        {
            // Arrange
            var manager = this.CreateManager();
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
            Assert.Equal(twin, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task GetTwinAsync()
        {
            // Arrange
            var manager = this.CreateManager();
            var deviceId = "deviceid";
            var twin = new Twin(deviceId);

            this.mockRegistryManager.Setup(c => c.GetTwinAsync(
                It.Is<string>(x => x == deviceId)))
                .ReturnsAsync(twin);

            // Act
            var result = await manager.GetTwinAsync(deviceId);

            // Assert
            Assert.Equal(twin, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task UpdateTwinAsync_With_Module()
        {
            // Arrange
            var manager = this.CreateManager();
            var deviceId = "deviceid";
            var moduleId = "moduleid";
            var deviceTwin = new Twin();
            var eTag = "eTag";

            this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                It.Is<string>(x => x == deviceId),
                It.Is<string>(x => x == moduleId),
                It.Is<Twin>(x => x == deviceTwin),
                It.Is<string>(x => x == eTag)))
                .ReturnsAsync(deviceTwin);

            // Act
            var result = await manager.UpdateTwinAsync(deviceId, moduleId, deviceTwin, eTag);

            // Assert
            Assert.Equal(deviceTwin, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task UpdateTwinAsync_With_CancellationToken()
        {
            // Arrange
            var manager = this.CreateManager();
            var deviceId = "deviceid";
            var deviceTwin = new Twin();
            var eTag = "eTag";
            var cancellationToken = CancellationToken.None;

            this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                It.Is<string>(x => x == deviceId),
                It.Is<Twin>(x => x == deviceTwin),
                It.Is<string>(x => x == eTag),
                It.Is<CancellationToken>(x => x == cancellationToken)))
                .ReturnsAsync(deviceTwin);

            // Act
            var result = await manager.UpdateTwinAsync(deviceId, deviceTwin, eTag, cancellationToken);

            // Assert
            Assert.Equal(deviceTwin, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task UpdateTwinAsync2()
        {
            // Arrange
            var manager = this.CreateManager();
            var deviceId = "deviceid";
            var deviceTwin = new Twin();
            var eTag = "eTag";

            this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                It.Is<string>(x => x == deviceId),
                It.Is<Twin>(x => x == deviceTwin),
                It.Is<string>(x => x == eTag)))
                .ReturnsAsync(deviceTwin);

            // Act
            var result = await manager.UpdateTwinAsync(deviceId, deviceTwin, eTag);

            // Assert
            Assert.Equal(deviceTwin, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task UpdateTwinAsync_With_Module_And_CancellationToken()
        {
            // Arrange
            var manager = this.CreateManager();
            var deviceId = "deviceid";
            var moduleId = "moduleid";
            var deviceTwin = new Twin();
            var eTag = "eTag";
            var cancellationToken = CancellationToken.None;

            this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                It.Is<string>(x => x == deviceId),
                It.Is<string>(x => x == moduleId),
                It.Is<Twin>(x => x == deviceTwin),
                It.Is<string>(x => x == eTag),
                It.Is<CancellationToken>(x => x == cancellationToken)))
                .ReturnsAsync(deviceTwin);

            // Act
            var result = await manager.UpdateTwinAsync(
                deviceId,
                moduleId,
                deviceTwin,
                eTag,
                cancellationToken);

            // Assert
            Assert.Equal(deviceTwin, result);
            this.mockRepository.VerifyAll();
        }

        [Fact]
        public async Task RemoveDeviceAsync()
        {
            // Arrange
            var manager = this.CreateManager();
            var deviceId = "deviceid";

            this.mockRegistryManager.Setup(c => c.RemoveDeviceAsync(
                It.Is<string>(x => x == deviceId)))
                .Returns(Task.CompletedTask);

            // Act
            await manager.RemoveDeviceAsync(
                deviceId);

            // Assert
            this.mockRepository.VerifyAll();
        }
    }
}
