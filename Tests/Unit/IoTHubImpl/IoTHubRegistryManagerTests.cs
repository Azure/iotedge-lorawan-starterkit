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
    using Azure;
    using Bogus.DataSets;
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

        [Fact]
        public async Task AddDevice()
        {
            // Arrange
            using var manager = CreateManager();
            var devEUI = new DevEui(123456789);
            var mockTwin = new Twin(devEUI.ToString());

            var mockDeviceTwin = new IoTHubDeviceTwin(mockTwin);

            mockRegistryManager.Setup(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == devEUI.ToString()), It.Is<Twin>(t => t == mockTwin)))
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = true
                });

            // Act
            var result = await manager.AddDevice(mockDeviceTwin);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task WhenBulkOperationFailed_AddDevice_Should_Return_False()
        {
            // Arrange
            using var manager = CreateManager();
            var devEUI = new DevEui(123456789);
            var mockTwin = new Twin(devEUI.ToString());

            var mockDeviceTwin = new IoTHubDeviceTwin(mockTwin);

            mockRegistryManager.Setup(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == devEUI.ToString()), It.Is<Twin>(t => t == mockTwin)))
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = false
                });

            // Act
            var result = await manager.AddDevice(mockDeviceTwin);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("2", "1", "3", "publishUserName", "publishPassword")]
        [InlineData("2", "1", "3", "fakeUser", "fakePassword", "fakeNetworkId")]
        [InlineData("2", "1", "3", "fakeUser", "fakePassword", "fakeNetworkId", "ws://fakelns:5000")]
        public async Task DeployEdgeDevice(string resetPin,
                string spiSpeed,
                string spiDev,
                string publishingUserName,
                string publishingPassword,
                string networkId = Constants.NetworkId,
                string lnsHostAddress = "ws://mylns:5000")
        {
            // Arrange
            using var manager = CreateManager();
            ConfigurationContent configurationContent = null;
            Twin networkServerModuleTwin = null;

            var deviceId = this.SetupForEdgeDeployment(
                    publishingUserName,
                    publishingPassword,
                    (string _, ConfigurationContent content) => configurationContent = content,
                    (string _, string _, Twin t, string _) => networkServerModuleTwin = t);

            // Act
            await manager.DeployEdgeDevice(deviceId, resetPin, spiSpeed, spiDev, publishingUserName, publishingPassword, networkId, lnsHostAddress);

            // Assert
            Assert.Equal($"{{\"modulesContent\":{{\"$edgeAgent\":{{\"properties.desired\":{{\"modules\":{{\"LoRaBasicsStationModule\":{{\"env\":{{\"RESET_PIN\":{{\"value\":\"{resetPin}\"}},\"TC_URI\":{{\"value\":\"ws://172.17.0.1:5000\"}},\"SPI_DEV\":{{\"value\":\"{spiDev}\"}},\"SPI_SPEED\":{{\"value\":\"2\"}}}}}}}}}}}}}},\"moduleContent\":{{}},\"deviceContent\":{{}}}}", JsonConvert.SerializeObject(configurationContent));
            Assert.Equal($"{{\"deviceId\":null,\"etag\":null,\"version\":null,\"tags\":{{\"network\":\"{networkId}\"}},\"properties\":{{\"desired\":{{\"FacadeServerUrl\":\"https://fake-facade.azurewebsites.net/api/\",\"FacadeAuthCode\":\"uzW4cD3VH88di5UB8kr7U8Ri\",\"hostAddress\":\"{lnsHostAddress}\"}},\"reported\":{{}}}}}}", JsonConvert.SerializeObject(networkServerModuleTwin));

            this.mockRegistryManager.Verify(c => c.AddDeviceAsync(It.IsAny<Device>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.AddModuleAsync(It.IsAny<Module>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.ApplyConfigurationContentOnDeviceAsync(It.Is<string>(deviceId, StringComparer.OrdinalIgnoreCase), It.IsAny<ConfigurationContent>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.GetTwinAsync(It.Is<string>(deviceId, StringComparer.OrdinalIgnoreCase)), Times.Once);
            this.mockRegistryManager.Verify(c => c.UpdateTwinAsync(
                            It.Is(deviceId, StringComparer.OrdinalIgnoreCase),
                            It.Is("LoRaWanNetworkSrvModule", StringComparer.OrdinalIgnoreCase),
                            It.IsAny<Twin>(),
                            It.IsAny<string>()), Times.Once);

            this.mockHttpClientHandler.VerifyNoOutstandingRequest();
            this.mockHttpClientHandler.VerifyNoOutstandingExpectation();
        }

        [Fact]
        public async Task DeployEdgeDeviceWhenOmmitingSpiDevAndAndSpiSpeedSettingsAreNotSendToConfiguration()
        {
            var publishingUserName = RandomString(16);
            var publishingPassword = RandomString(24);

            // Arrange
            using var manager = CreateManager();
            ConfigurationContent configurationContent = null;
            Twin networkServerModuleTwin = null;

            var deviceId = this.SetupForEdgeDeployment(
                    publishingUserName,
                    publishingPassword,
                    (string _, ConfigurationContent content) => configurationContent = content,
                    (string _, string _, Twin t, string _) => networkServerModuleTwin = t);

            // Act
            await manager.DeployEdgeDevice(deviceId, "2", null, null, publishingUserName, publishingPassword, Constants.NetworkId, "ws://mylns:5000");

            // Assert
            Assert.Equal($"{{\"modulesContent\":{{\"$edgeAgent\":{{\"properties.desired\":{{\"modules\":{{\"LoRaBasicsStationModule\":{{\"env\":{{\"RESET_PIN\":{{\"value\":\"2\"}},\"TC_URI\":{{\"value\":\"ws://172.17.0.1:5000\"}}}}}}}}}}}}}},\"moduleContent\":{{}},\"deviceContent\":{{}}}}", JsonConvert.SerializeObject(configurationContent));

            this.mockRegistryManager.Verify(c => c.AddDeviceAsync(It.IsAny<Device>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.AddModuleAsync(It.IsAny<Module>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.ApplyConfigurationContentOnDeviceAsync(It.Is<string>(deviceId, StringComparer.OrdinalIgnoreCase), It.IsAny<ConfigurationContent>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.GetTwinAsync(It.Is<string>(deviceId, StringComparer.OrdinalIgnoreCase)), Times.Once);
            this.mockRegistryManager.Verify(c => c.UpdateTwinAsync(
                            It.Is(deviceId, StringComparer.OrdinalIgnoreCase),
                            It.Is("LoRaWanNetworkSrvModule", StringComparer.OrdinalIgnoreCase),
                            It.IsAny<Twin>(),
                            It.IsAny<string>()), Times.Once);

            this.mockHttpClientHandler.VerifyNoOutstandingRequest();
            this.mockHttpClientHandler.VerifyNoOutstandingExpectation();
        }

        [Fact]
        public async Task DeployEdgeDeviceSettingLogAnalyticsWorkspaceShouldDeployIotHubMetricsCollectorModule()
        {
            var publishingUserName = RandomString(16);
            var publishingPassword = RandomString(24);

            // Arrange
            using var manager = CreateManager();
            ConfigurationContent configurationContent = null;
            Configuration iotHubMetricsCollectorModuleConfiguration = null;

            Twin networkServerModuleTwin = null;

            Environment.SetEnvironmentVariable("LOG_ANALYTICS_WORKSPACE_ID", "fake-workspace-id");
            Environment.SetEnvironmentVariable("IOT_HUB_RESOURCE_ID", "fake-hub-id");
            Environment.SetEnvironmentVariable("LOG_ANALYTICS_WORKSPACE_KEY", "fake-workspace-key");
            Environment.SetEnvironmentVariable("OBSERVABILITY_CONFIG_LOCATION", "https://fake.local/observabilityConfig.json");

            var deviceId = this.SetupForEdgeDeployment(
                    publishingUserName,
                    publishingPassword,
                    (string _, ConfigurationContent content) => configurationContent = content,
                    (string _, string _, Twin t, string _) => networkServerModuleTwin = t);

            this.mockRegistryManager.Setup(c => c.AddModuleAsync(It.Is<Module>(m => m.DeviceId == deviceId && m.Id == "IotHubMetricsCollectorModule")))
                .ReturnsAsync((Module m) => m);

            this.mockRegistryManager.Setup(c => c.AddConfigurationAsync(It.Is<Configuration>(conf => conf.TargetCondition == $"deviceId='{deviceId}'")))
                .ReturnsAsync((Configuration c) => c)
                .Callback((Configuration c) => iotHubMetricsCollectorModuleConfiguration = c);

#pragma warning disable JSON001 // Invalid JSON pattern
            _ = this.mockHttpClientHandler.When(HttpMethod.Get, "/observabilityConfig.json")
                .Respond(HttpStatusCode.OK, MediaTypeNames.Application.Json, /*lang=json,strict*/ "{\"modulesContent\":{\"$edgeAgent\":{\"properties.desired.modules.IotHubMetricsCollectorModule\":{\"settings\":{\"image\":\"mcr.microsoft.com/azureiotedge-metrics-collector:1.0\"},\"type\":\"docker\",\"env\":{\"ResourceId\":{\"value\":\"[$iot_hub_resource_id]\"},\"UploadTarget\":{\"value\":\"AzureMonitor\"},\"LogAnalyticsWorkspaceId\":{\"value\":\"[$log_analytics_workspace_id]\"},\"LogAnalyticsSharedKey\":{\"value\":\"[$log_analytics_shared_key]\"},\"MetricsEndpointsCSV\":{\"value\":\"http://edgeHub:9600/metrics,http://edgeAgent:9600/metrics\"}},\"status\":\"running\",\"restartPolicy\":\"always\",\"version\":\"1.0\"}}}}");
#pragma warning restore JSON001 // Invalid JSON pattern

            // Act
            await manager.DeployEdgeDevice(deviceId, "2", null, null, publishingUserName, publishingPassword, Constants.NetworkId, "ws://mylns:5000");

            // Assert
            Assert.Equal(/*lang=json,strict*/ "{\"modulesContent\":{\"$edgeAgent\":{\"properties.desired\":{\"modules\":{\"LoRaBasicsStationModule\":{\"env\":{\"RESET_PIN\":{\"value\":\"2\"},\"TC_URI\":{\"value\":\"ws://172.17.0.1:5000\"}}}}}}},\"moduleContent\":{},\"deviceContent\":{}}", JsonConvert.SerializeObject(configurationContent));
            Assert.Equal(/*lang=json,strict*/ "{\"modulesContent\":{\"$edgeAgent\":{\"properties.desired.modules.IotHubMetricsCollectorModule\":{\"settings\":{\"image\":\"mcr.microsoft.com/azureiotedge-metrics-collector:1.0\"},\"type\":\"docker\",\"env\":{\"ResourceId\":{\"value\":\"fake-hub-id\"},\"UploadTarget\":{\"value\":\"AzureMonitor\"},\"LogAnalyticsWorkspaceId\":{\"value\":\"fake-workspace-id\"},\"LogAnalyticsSharedKey\":{\"value\":\"fake-workspace-key\"},\"MetricsEndpointsCSV\":{\"value\":\"http://edgeHub:9600/metrics,http://edgeAgent:9600/metrics\"}},\"status\":\"running\",\"restartPolicy\":\"always\",\"version\":\"1.0\"}}},\"moduleContent\":{},\"deviceContent\":{}}", JsonConvert.SerializeObject(iotHubMetricsCollectorModuleConfiguration.Content));

            this.mockRegistryManager.Verify(c => c.AddDeviceAsync(It.IsAny<Device>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.AddModuleAsync(It.IsAny<Module>()), Times.Exactly(2));
            this.mockRegistryManager.Verify(c => c.AddModuleAsync(It.Is<Module>(m => m.DeviceId == deviceId && m.Id == "IotHubMetricsCollectorModule")), Times.Once);
            this.mockRegistryManager.Verify(c => c.AddConfigurationAsync(It.Is<Configuration>(conf => conf.TargetCondition == $"deviceId='{deviceId}'")), Times.Once);

            this.mockRegistryManager.Verify(c => c.ApplyConfigurationContentOnDeviceAsync(It.Is<string>(deviceId, StringComparer.OrdinalIgnoreCase), It.IsAny<ConfigurationContent>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.GetTwinAsync(It.Is<string>(deviceId, StringComparer.OrdinalIgnoreCase)), Times.Once);
            this.mockRegistryManager.Verify(c => c.UpdateTwinAsync(
                            It.Is(deviceId, StringComparer.OrdinalIgnoreCase),
                            It.Is("LoRaWanNetworkSrvModule", StringComparer.OrdinalIgnoreCase),
                            It.IsAny<Twin>(),
                            It.IsAny<string>()), Times.Once);

            this.mockHttpClientHandler.VerifyNoOutstandingRequest();
            this.mockHttpClientHandler.VerifyNoOutstandingExpectation();
        }

        [Theory]
        [InlineData("EU")]
        [InlineData("US")]
        [InlineData("EU", "fakeNetwork")]
        [InlineData("US", "fakeNetwork")]
        public async Task DeployConcentrator(string region, string networkId = Constants.NetworkId)
        {
            // Arrange
            using var manager = CreateManager();
            Environment.SetEnvironmentVariable("EU863_CONFIG_LOCATION", "https://fake.local/eu863.config.json");
            Environment.SetEnvironmentVariable("US902_CONFIG_LOCATION", "https://fake.local/us902.config.json");
            const string stationEui = "123456789";
            var eTag = $"{DateTime.Now.Ticks}";

            this.mockHttpClientFactory.Setup(c => c.CreateClient(It.IsAny<string>()))
                .Returns(() => mockHttpClientHandler.ToHttpClient());

            _ = region switch
            {
                "EU" => this.mockHttpClientHandler.When(HttpMethod.Get, "/eu863.config.json")
                                        .Respond(HttpStatusCode.OK, MediaTypeNames.Application.Json, /*lang=json,strict*/ "{\"config\":\"EU\"}"),
                "US" => this.mockHttpClientHandler.When(HttpMethod.Get, "/us902.config.json")
                                        .Respond(HttpStatusCode.OK, MediaTypeNames.Application.Json, /*lang=json,strict*/ "{\"config\":\"US\"}"),
                _ => throw new ArgumentException($"{region} is not supported."),
            };

            this.mockRegistryManager.Setup(c => c.AddDeviceAsync(It.Is<Device>(d => d.Id == stationEui)))
                .ReturnsAsync((Device d) => d);

            _ = this.mockRegistryManager.Setup(c => c.GetTwinAsync(It.Is<string>(stationEui, StringComparer.OrdinalIgnoreCase)))
                .ReturnsAsync(new Twin(stationEui)
                {
                    ETag = eTag
                });

            _ = this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                            It.Is(stationEui, StringComparer.OrdinalIgnoreCase),
                            It.IsAny<Twin>(),
                            It.Is(eTag, StringComparer.OrdinalIgnoreCase)))
                    .ReturnsAsync((string _, Twin t, string _) => t)
                    .Callback((string _, Twin t, string _) =>
                    {
                        Assert.Equal($"{{\"config\":\"{region}\"}}", JsonConvert.SerializeObject(t.Properties.Desired["routerConfig"]));
                        Assert.Equal($"\"{networkId}\"", JsonConvert.SerializeObject(t.Tags[Constants.NetworkTagName]));
                    });

            // Act
            await manager.DeployConcentrator(stationEui, region, networkId);

            // Assert
            this.mockRegistryManager.Verify(c => c.AddDeviceAsync(It.IsAny<Device>()), Times.Once);
            this.mockRegistryManager.Verify(c => c.GetTwinAsync(It.Is<string>(stationEui, StringComparer.OrdinalIgnoreCase)), Times.Once);
            this.mockRegistryManager.Verify(c => c.UpdateTwinAsync(
                            It.Is(stationEui, StringComparer.OrdinalIgnoreCase),
                            It.IsAny<Twin>(),
                            It.Is(eTag, StringComparer.OrdinalIgnoreCase)), Times.Once);

            this.mockHttpClientHandler.VerifyNoOutstandingRequest();
            this.mockHttpClientHandler.VerifyNoOutstandingExpectation();
        }

        [Fact]
        public async Task DeployConcentratorWithNotImplementedRegionShouldThrowSwitchExpressionException()
        {
            // Arrange
            using var manager = CreateManager();

            this.mockHttpClientFactory.Setup(c => c.CreateClient(It.IsAny<string>()))
                .Returns(() => mockHttpClientHandler.ToHttpClient());

            // Act
            _ = await Assert.ThrowsAsync<SwitchExpressionException>(() => manager.DeployConcentrator("123456789", "FAKE"));
        }

        [Fact]
        public async Task DeployEndDevices()
        {
            // Arrange
            using var manager = CreateManager();

            Dictionary<string, Twin> deviceTwins = new();
            var eTag = $"{DateTime.Now.Ticks}";

            this.mockRegistryManager.Setup(c => c.AddDeviceAsync(It.IsAny<Device>()))
                .ReturnsAsync((Device d) => d);

            this.mockRegistryManager.Setup(c => c.GetTwinAsync(It.IsAny<string>()))
                .ReturnsAsync((string id) => new Twin(id)
                {
                    ETag = eTag
                });

            this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(It.IsAny<string>(), It.IsAny<Twin>(), It.Is(eTag, StringComparer.OrdinalIgnoreCase)))
                .ReturnsAsync((string _, Twin t, string _) => t)
                .Callback((string id, Twin t, string _) => deviceTwins.Add(id, t));

            // Act
            var result = await manager.DeployEndDevices();

            // Assert
            Assert.True(result);
            var abpDevice = deviceTwins[Constants.AbpDeviceId];
            var otaaDevice = deviceTwins[Constants.OtaaDeviceId];

            Assert.Equal(/*lang=json*/ "{\"AppEUI\":\"BE7A0000000014E2\",\"AppKey\":\"8AFE71A145B253E49C3031AD068277A1\",\"GatewayID\":\"\",\"SensorDecoder\":\"DecoderValueSensor\"}", JsonConvert.SerializeObject(otaaDevice.Properties.Desired));
            Assert.Equal(/*lang=json*/ "{\"AppSKey\":\"2B7E151628AED2A6ABF7158809CF4F3C\",\"NwkSKey\":\"3B7E151628AED2A6ABF7158809CF4F3C\",\"GatewayID\":\"\",\"DevAddr\":\"0228B1B1\",\"SensorDecoder\":\"DecoderValueSensor\"}", JsonConvert.SerializeObject(abpDevice.Properties.Desired));

            this.mockRegistryManager.Verify(c => c.AddDeviceAsync(It.IsAny<Device>()), Times.Exactly(2));
            this.mockRegistryManager.Verify(c => c.GetTwinAsync(It.IsAny<string>()), Times.Exactly(2));
            this.mockRegistryManager.Verify(c => c.UpdateTwinAsync(It.IsAny<string>(), It.IsAny<Twin>(), It.IsAny<string>()), Times.Exactly(2));
        }

        private string SetupForEdgeDeployment(string publishingUserName, string publishingPassword,
                        Action<string, ConfigurationContent> onApplyConfigurationContentOnDevice,
                        Func<string, string, Twin, string, Twin> onUpdateLoRaWanNetworkServerModuleTwin)
        {
            this.mockHttpClientFactory.Setup(c => c.CreateClient(It.IsAny<string>()))
                .Returns(() => mockHttpClientHandler.ToHttpClient());

            const string deviceId = "edgeTest";
            var eTag = $"{DateTime.Now.Ticks}";

            Environment.SetEnvironmentVariable("FACADE_HOST_NAME", "fake-facade");
            Environment.SetEnvironmentVariable("WEBSITE_CONTENTSHARE", "fake.local");
            Environment.SetEnvironmentVariable("DEVICE_CONFIG_LOCATION", "https://fake.local/deviceConfiguration.json");

            this.mockRegistryManager.Setup(c => c.AddDeviceAsync(It.Is<Device>(d => d.Id == deviceId && d.Capabilities.IotEdge)))
                .ReturnsAsync((Device d) => d);

            this.mockRegistryManager.Setup(c => c.AddModuleAsync(It.Is<Module>(m => m.DeviceId == deviceId && m.Id == "LoRaWanNetworkSrvModule")))
                .ReturnsAsync((Module m) => m);

            _ = this.mockHttpClientHandler.When(HttpMethod.Get, "/api/functions/admin/token")
                .With(c =>
                {
                    Assert.Equal("Basic", c.Headers.Authorization.Scheme);
                    Assert.Equal(Convert.ToBase64String(Encoding.Default.GetBytes($"{publishingUserName}:{publishingPassword}")), c.Headers.Authorization.Parameter);

                    return true;
                })
                .Respond(HttpStatusCode.OK, MediaTypeNames.Text.Plain, "JWT-BEARER-TOKEN");

            _ = this.mockHttpClientHandler.When(HttpMethod.Get, "/admin/host/keys")
                .With(c =>
                {
                    Assert.Equal("Bearer", c.Headers.Authorization.Scheme);
                    Assert.Equal("JWT-BEARER-TOKEN", c.Headers.Authorization.Parameter);
                    return true;
                })
                .Respond(HttpStatusCode.OK, MediaTypeNames.Application.Json, /*lang=json,strict*/ "{\"keys\":[{\"name\":\"default\",\"value\":\"uzW4cD3VH88di5UB8kr7U8Ri\"},{\"name\":\"master\",\"value\":\"4bF86stCFr7ga8A7j59XEYnX\"}]}");

#pragma warning disable JSON001 // Invalid JSON pattern
            _ = this.mockHttpClientHandler.When(HttpMethod.Get, "/deviceConfiguration.json")
                .Respond(HttpStatusCode.OK, MediaTypeNames.Application.Json, /*lang=json,strict*/ "{\"modulesContent\":{\"$edgeAgent\":{\"properties.desired\":{\"modules\":{\"LoRaBasicsStationModule\":{\"env\":{\"RESET_PIN\":{\"value\":\"[$reset_pin]\"},\"TC_URI\":{\"value\":\"ws://172.17.0.1:5000\"}[$spi_dev][$spi_speed]}}}}}}}");
#pragma warning restore JSON001 // Invalid JSON pattern

            _ = this.mockRegistryManager.Setup(c => c.ApplyConfigurationContentOnDeviceAsync(It.Is<string>(deviceId, StringComparer.OrdinalIgnoreCase), It.IsAny<ConfigurationContent>()))
                .Callback(onApplyConfigurationContentOnDevice)
                .Returns(Task.CompletedTask);

            _ = this.mockRegistryManager.Setup(c => c.GetTwinAsync(It.Is<string>(deviceId, StringComparer.OrdinalIgnoreCase)))
                .ReturnsAsync(new Twin(deviceId)
                {
                    ETag = eTag
                });

            _ = this.mockRegistryManager.Setup(c => c.UpdateTwinAsync(
                            It.Is(deviceId, StringComparer.OrdinalIgnoreCase),
                            It.Is("LoRaWanNetworkSrvModule", StringComparer.OrdinalIgnoreCase),
                            It.IsAny<Twin>(),
                            It.Is(eTag, StringComparer.OrdinalIgnoreCase)))
                    .ReturnsAsync(onUpdateLoRaWanNetworkServerModuleTwin);

            return deviceId;
        }

        private static string RandomString(int size)
        {
            var rand = new Random();

            int randValue;
            var str = "";
            char letter;

            for (var i = 0; i < size; i++)
            {
                // Generating a random number.
                randValue = rand.Next(0, 26);

                // Generating random character by converting
                // the random number into character.
                letter = Convert.ToChar(randValue + 65);

                // Appending the letter to string.
                str += letter;
            }

            return str;
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
