// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Tests.Unit
{
    using System.Globalization;
    using LoRaWan.Tools.CLI.Helpers;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public class DeviceProvisioningTest
    {
        private readonly ConfigurationHelper configurationHelper;
        private readonly Mock<RegistryManager> registryManager;

        private const string NetworkName = "quickstartnetwork";
        private static readonly Random Random = new Random();

        private static readonly string DevEUI = GetRandomHexNumber(16);
        private const string Decoder = "DecoderValueSensor";
        private const string LoRaVersion = "999.999.10"; // using an non-existing version to ensure it is not hardcoded with a valid value
        private const string IotEdgeVersion = "1.4";

        // OTAA Properties
        private static readonly string AppKey = GetRandomHexNumber(32);
        private static readonly string AppEui = GetRandomHexNumber(16);

        // ABP properties
        private static readonly string AppSKey = GetRandomHexNumber(32);
        private static readonly string NwkSKey = GetRandomHexNumber(32);
        private const string DevAddr = "027AEC7B";
        public static string GetRandomHexNumber(int digits)
        {
            return string.Concat(Enumerable.Range(0, digits).Select(_ => Random.Next(16).ToString("X", CultureInfo.InvariantCulture)));
        }
        public DeviceProvisioningTest()
        {
            this.registryManager = new Mock<RegistryManager>();
            this.configurationHelper = new ConfigurationHelper
            {
                NetId = ValidationHelper.CleanNetId(Constants.DefaultNetId.ToString(CultureInfo.InvariantCulture)),
                RegistryManager = this.registryManager.Object
            };
        }

        private static string[] CreateArgs(string input)
        {
            return input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static IDictionary<string, object> GetConcentratorRouterConfig(string region)
        {
            if (region is null) throw new ArgumentNullException(nameof(region));
            var fileName = Path.Combine(IoTDeviceHelper.DefaultRouterConfigFolder, $"{region.ToUpperInvariant()}.json");
            var raw = File.ReadAllText(fileName);

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(raw)!;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task AddABPDevice(bool deviceExistsInRegistry)
        {
            // Arrange            
            var savedTwin = new Twin();

            this.registryManager.Setup(c => c.AddDeviceWithTwinAsync(
                    It.Is<Device>(d => d.Id == DevEUI),
                    It.IsNotNull<Twin>()))
                .Callback((Device d, Twin t) =>
                {
                    Assert.Equal(NetworkName, t.Tags[DeviceTags.NetworkTagName].ToString());
                    Assert.Equal(new string[] { DeviceTags.DeviceTypes.Leaf }, ((JArray)t.Tags[DeviceTags.DeviceTypeTagName]).Select(x => x.ToString()).ToArray());
                    Assert.Equal(AppSKey, t.Properties.Desired[TwinProperty.AppSKey].ToString());
                    Assert.Equal(NwkSKey, t.Properties.Desired[TwinProperty.NwkSKey].ToString());
                    Assert.Equal(DevAddr, t.Properties.Desired[TwinProperty.DevAddr].ToString());
                    Assert.Equal(Decoder, t.Properties.Desired[TwinProperty.SensorDecoder].ToString());
                    Assert.Equal(string.Empty, t.Properties.Desired[TwinProperty.GatewayID].ToString());
                    savedTwin = t;
                })
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = true
                });

            // If the device exists in registry we expect the getTwin to return the device.
            if (deviceExistsInRegistry)
            {
                this.registryManager.Setup(x => x.GetTwinAsync(DevEUI)).ReturnsAsync(savedTwin);
            }
            else
            {
                this.registryManager.SetupSequence(x => x.GetTwinAsync(DevEUI))
                    .ReturnsAsync((Twin?)null)
                    .ReturnsAsync(savedTwin);
            }

            // Act
            var args = CreateArgs($"add --type abp --deveui {DevEUI} --appskey {AppSKey} --nwkskey {NwkSKey} --devaddr {DevAddr} --decoder {Decoder} --network {NetworkName}");
            var actual = await Program.Run(args, this.configurationHelper);

            // Assert
            Assert.Equal(0, actual);

            if (deviceExistsInRegistry)
            {
                this.registryManager.Verify(c => c.UpdateTwinAsync(
                        DevEUI,
                        It.IsNotNull<Twin>(),
                        It.IsAny<string>()), Times.Once());
                this.registryManager.Verify(c => c.AddDeviceWithTwinAsync(
                        It.IsAny<Device>(),
                        It.IsAny<Twin>()), Times.Never());
            }
            else
            {
                this.registryManager.Verify(c => c.AddDeviceWithTwinAsync(
                        It.Is<Device>(d => d.Id == DevEUI),
                        It.IsNotNull<Twin>()), Times.Once());
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task AddOTAADevice(bool deviceExistsInRegistry)
        {
            // Arrange            
            var savedTwin = new Twin();

            this.registryManager.Setup(c => c.AddDeviceWithTwinAsync(
                    It.Is<Device>(d => d.Id == DevEUI),
                    It.IsNotNull<Twin>()))
                .Callback((Device d, Twin t) =>
                {
                    Assert.Equal(NetworkName, t.Tags[DeviceTags.NetworkTagName].ToString());
                    Assert.Equal(new string[] { DeviceTags.DeviceTypes.Leaf }, ((JArray)t.Tags[DeviceTags.DeviceTypeTagName]).Select(x => x.ToString()).ToArray());
                    Assert.Equal(AppKey, t.Properties.Desired[TwinProperty.AppKey].ToString());
                    Assert.Equal(AppEui, t.Properties.Desired[TwinProperty.AppEUI].ToString());
                    Assert.Equal(Decoder, t.Properties.Desired[TwinProperty.SensorDecoder].ToString());
                    Assert.Equal(string.Empty, t.Properties.Desired[TwinProperty.GatewayID].ToString());
                    savedTwin = t;
                })
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = true
                });

            // If the device exists in registry we expect the getTwin to return the device.
            if (deviceExistsInRegistry)
            {
                this.registryManager.Setup(x => x.GetTwinAsync(DevEUI)).ReturnsAsync(savedTwin);
            }
            else
            {
                this.registryManager.SetupSequence(x => x.GetTwinAsync(DevEUI))
                    .ReturnsAsync((Twin?)null)
                    .ReturnsAsync(savedTwin);
            }

            // Act
            var args = CreateArgs($"add --type otaa --deveui {DevEUI} --appeui {AppEui} --appkey {AppKey}  --decoder {Decoder} --network {NetworkName}");
            var actual = await Program.Run(args, this.configurationHelper);

            // Assert
            Assert.Equal(0, actual);
            if (deviceExistsInRegistry)
            {
                this.registryManager.Verify(c => c.UpdateTwinAsync(
                        DevEUI,
                        It.IsNotNull<Twin>(),
                        It.IsAny<string>()), Times.Once());
                this.registryManager.Verify(c => c.AddDeviceWithTwinAsync(
                        It.IsAny<Device>(),
                        It.IsAny<Twin>()), Times.Never());
            }
            else
            {
                this.registryManager.Verify(c => c.AddDeviceWithTwinAsync(
                        It.Is<Device>(d => d.Id == DevEUI),
                        It.IsNotNull<Twin>()), Times.Once());
            }
        }

        [Fact]

        public async Task WhenBulkOperationFailed_AddDevice_Should_Return_False()
        {
            // Arrange
            this.registryManager.Setup(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == DevEUI), It.IsNotNull<Twin>()))
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = false
                });

            // Act
            var args = CreateArgs($"add --type otaa --deveui {DevEUI} --appeui 8AFE71A145B253E49C3031AD068277A1 --appkey BE7A0000000014E2 --decoder MyDecoder --network myNetwork");
            var actual = await Program.Run(args, this.configurationHelper);

            // Assert
            Assert.Equal(-1, actual);
        }

        [Theory]    
        [InlineData("2", "1", "3")]
        [InlineData("2", "1", "3", "fakeNetworkId")]
        [InlineData("2", "1", "3", "fakeNetworkId", "ws://fakelns:5000")]
        public async Task DeployEdgeDevice(
                string resetPin,
                string spiSpeed,
                string spiDev,
                string networkId = NetworkName,
                string lnsHostAddress = "ws://mylns:5000")
        {
            // Arrange
            const string deviceId = "myGateway";
            const string facadeURL = "https://myfunc.azurewebsites.com/api";
            const string facadeAuthCode = "secret-code";

            ConfigurationContent? actualConfiguration = null;
            this.registryManager.Setup(x => x.ApplyConfigurationContentOnDeviceAsync(deviceId, It.IsNotNull<ConfigurationContent>()))
                .Callback((string deviceId, ConfigurationContent c) => actualConfiguration = c);

            this.registryManager.Setup(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == deviceId && d.Capabilities.IotEdge), It.IsNotNull<Twin>()))
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = true
                });

            var actualSpiSpeed = 2;

            // Act
            var args = CreateArgs($"add-gateway --reset-pin {resetPin} --device-id {deviceId} --spi-dev {spiDev} --spi-speed {spiSpeed} --api-url {facadeURL} --api-key {facadeAuthCode} --lns-host-address {lnsHostAddress} --network {networkId} --lora-version {LoRaVersion}");
            var actual = await Program.Run(args, this.configurationHelper);

            // Assert
            Assert.Equal(0, actual);
            this.registryManager.Verify(x => x.ApplyConfigurationContentOnDeviceAsync(deviceId, It.IsNotNull<ConfigurationContent>()), Times.Once);
            this.registryManager.Verify(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == deviceId && d.Capabilities.IotEdge), It.IsNotNull<Twin>()), Times.Once);

            // Should not deploy monitoring layer
            this.registryManager.Verify(x => x.AddConfigurationAsync(It.IsNotNull<Configuration>()), Times.Never);

            var actualConfigurationJson = JsonConvert.SerializeObject(actualConfiguration);
            var expectedConfigurationJson = $"{{\"modulesContent\":{{\"$edgeAgent\":{{\"properties.desired\":{{\"schemaVersion\":\"1.0\",\"runtime\":{{\"type\":\"docker\",\"settings\":{{\"loggingOptions\":\"\",\"minDockerVersion\":\"v1.25\"}}}},\"systemModules\":{{\"edgeAgent\":{{\"type\":\"docker\",\"settings\":{{\"image\":\"mcr.microsoft.com/azureiotedge-agent:{IotEdgeVersion}\",\"createOptions\":\"{{}}\"}}}},\"edgeHub\":{{\"type\":\"docker\",\"settings\":{{\"image\":\"mcr.microsoft.com/azureiotedge-hub:{IotEdgeVersion}\",\"createOptions\":\"{{ \\\"HostConfig\\\": {{   \\\"PortBindings\\\": {{\\\"8883/tcp\\\": [  {{\\\"HostPort\\\": \\\"8883\\\" }}  ], \\\"443/tcp\\\": [ {{ \\\"HostPort\\\": \\\"443\\\" }} ], \\\"5671/tcp\\\": [ {{ \\\"HostPort\\\": \\\"5671\\\"  }}] }} }}}}\"}},\"env\":{{\"OptimizeForPerformance\":{{\"value\":\"false\"}},\"mqttSettings__enabled\":{{\"value\":\"false\"}},\"AuthenticationMode\":{{\"value\":\"CloudAndScope\"}},\"NestedEdgeEnabled\":{{\"value\":\"false\"}}}},\"status\":\"running\",\"restartPolicy\":\"always\"}}}},\"modules\":{{\"LoRaWanNetworkSrvModule\":{{\"type\":\"docker\",\"settings\":{{\"image\":\"loraedge/lorawannetworksrvmodule:{LoRaVersion}\",\"createOptions\":\"{{\\\"ExposedPorts\\\": {{ \\\"5000/tcp\\\": {{}}}}, \\\"HostConfig\\\": {{  \\\"PortBindings\\\": {{\\\"5000/tcp\\\": [  {{ \\\"HostPort\\\": \\\"5000\\\", \\\"HostIp\\\":\\\"172.17.0.1\\\" }} ]}}}}}}\"}},\"version\":\"1.0\",\"env\":{{\"ENABLE_GATEWAY\":{{\"value\":\"true\"}},\"LOG_LEVEL\":{{\"value\":\"2\"}}}},\"status\":\"running\",\"restartPolicy\":\"always\"}},\"LoRaBasicsStationModule\":{{\"type\":\"docker\",\"settings\":{{\"image\":\"loraedge/lorabasicsstationmodule:{LoRaVersion}\",\"createOptions\":\"  {{\\\"HostConfig\\\": {{\\\"NetworkMode\\\": \\\"host\\\", \\\"Privileged\\\": true }},  \\\"NetworkingConfig\\\": {{\\\"EndpointsConfig\\\": {{\\\"host\\\": {{}} }}}}}}\"}},\"env\":{{\"RESET_PIN\":{{\"value\":\"{resetPin}\"}},\"TC_URI\":{{\"value\":\"ws://172.17.0.1:5000\"}},\"SPI_DEV\":{{\"value\":\"{spiDev}\"}},\"SPI_SPEED\":{{\"value\":\"{actualSpiSpeed}\"}}}},\"version\":\"1.0\",\"status\":\"running\",\"restartPolicy\":\"always\"}}}}}}}},\"$edgeHub\":{{\"properties.desired\":{{\"schemaVersion\":\"1.0\",\"routes\":{{\"route\":\"FROM /* INTO $upstream\"}},\"storeAndForwardConfiguration\":{{\"timeToLiveSecs\":7200}}}}}},\"LoRaWanNetworkSrvModule\":{{\"properties.desired\":{{\"FacadeServerUrl\":\"{facadeURL}\",\"FacadeAuthCode\":\"{facadeAuthCode}\",\"hostAddress\":\"{lnsHostAddress}\",\"network\":\"{networkId}\"}}}}}},\"moduleContent\":{{}},\"deviceContent\":{{}}}}";
            Assert.Equal(expectedConfigurationJson, actualConfigurationJson);
        }

        [Fact]
        public async Task DeployEdgeDeviceWhenOmmitingSpiDevAndAndSpiSpeedSettingsAreNotSendToConfiguration()
        {
            // Arrange
            const string deviceId = "myGateway";
            const string facadeURL = "https://myfunc.azurewebsites.com/api";
            const string facadeAuthCode = "secret-code";
            const string lnsHostAddress = "ws://mylns:5000";
            const int resetPin = 2;

            ConfigurationContent? actualConfiguration = null;
            this.registryManager.Setup(x => x.ApplyConfigurationContentOnDeviceAsync(deviceId, It.IsNotNull<ConfigurationContent>()))
                .Callback((string deviceId, ConfigurationContent c) => actualConfiguration = c);

            this.registryManager.Setup(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == deviceId && d.Capabilities.IotEdge), It.IsNotNull<Twin>()))
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = true
                });

            // Act
            var args = CreateArgs($"add-gateway --reset-pin {resetPin} --device-id {deviceId}  --api-url {facadeURL} --api-key {facadeAuthCode} --lns-host-address {lnsHostAddress} --network {NetworkName} --lora-version {LoRaVersion}");
            var actual = await Program.Run(args, this.configurationHelper);

            // Assert
            Assert.Equal(0, actual);
            this.registryManager.Verify(x => x.ApplyConfigurationContentOnDeviceAsync(deviceId, It.IsNotNull<ConfigurationContent>()), Times.Once);
            this.registryManager.Verify(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == deviceId && d.Capabilities.IotEdge), It.IsNotNull<Twin>()), Times.Once);

            var actualConfigurationJson = JsonConvert.SerializeObject(actualConfiguration);
            var expectedConfigurationJson = $"{{\"modulesContent\":{{\"$edgeAgent\":{{\"properties.desired\":{{\"schemaVersion\":\"1.0\",\"runtime\":{{\"type\":\"docker\",\"settings\":{{\"loggingOptions\":\"\",\"minDockerVersion\":\"v1.25\"}}}},\"systemModules\":{{\"edgeAgent\":{{\"type\":\"docker\",\"settings\":{{\"image\":\"mcr.microsoft.com/azureiotedge-agent:{IotEdgeVersion}\",\"createOptions\":\"{{}}\"}}}},\"edgeHub\":{{\"type\":\"docker\",\"settings\":{{\"image\":\"mcr.microsoft.com/azureiotedge-hub:{IotEdgeVersion}\",\"createOptions\":\"{{ \\\"HostConfig\\\": {{   \\\"PortBindings\\\": {{\\\"8883/tcp\\\": [  {{\\\"HostPort\\\": \\\"8883\\\" }}  ], \\\"443/tcp\\\": [ {{ \\\"HostPort\\\": \\\"443\\\" }} ], \\\"5671/tcp\\\": [ {{ \\\"HostPort\\\": \\\"5671\\\"  }}] }} }}}}\"}},\"env\":{{\"OptimizeForPerformance\":{{\"value\":\"false\"}},\"mqttSettings__enabled\":{{\"value\":\"false\"}},\"AuthenticationMode\":{{\"value\":\"CloudAndScope\"}},\"NestedEdgeEnabled\":{{\"value\":\"false\"}}}},\"status\":\"running\",\"restartPolicy\":\"always\"}}}},\"modules\":{{\"LoRaWanNetworkSrvModule\":{{\"type\":\"docker\",\"settings\":{{\"image\":\"loraedge/lorawannetworksrvmodule:{LoRaVersion}\",\"createOptions\":\"{{\\\"ExposedPorts\\\": {{ \\\"5000/tcp\\\": {{}}}}, \\\"HostConfig\\\": {{  \\\"PortBindings\\\": {{\\\"5000/tcp\\\": [  {{ \\\"HostPort\\\": \\\"5000\\\", \\\"HostIp\\\":\\\"172.17.0.1\\\" }} ]}}}}}}\"}},\"version\":\"1.0\",\"env\":{{\"ENABLE_GATEWAY\":{{\"value\":\"true\"}},\"LOG_LEVEL\":{{\"value\":\"2\"}}}},\"status\":\"running\",\"restartPolicy\":\"always\"}},\"LoRaBasicsStationModule\":{{\"type\":\"docker\",\"settings\":{{\"image\":\"loraedge/lorabasicsstationmodule:{LoRaVersion}\",\"createOptions\":\"  {{\\\"HostConfig\\\": {{\\\"NetworkMode\\\": \\\"host\\\", \\\"Privileged\\\": true }},  \\\"NetworkingConfig\\\": {{\\\"EndpointsConfig\\\": {{\\\"host\\\": {{}} }}}}}}\"}},\"env\":{{\"RESET_PIN\":{{\"value\":\"{resetPin}\"}},\"TC_URI\":{{\"value\":\"ws://172.17.0.1:5000\"}}}},\"version\":\"1.0\",\"status\":\"running\",\"restartPolicy\":\"always\"}}}}}}}},\"$edgeHub\":{{\"properties.desired\":{{\"schemaVersion\":\"1.0\",\"routes\":{{\"route\":\"FROM /* INTO $upstream\"}},\"storeAndForwardConfiguration\":{{\"timeToLiveSecs\":7200}}}}}},\"LoRaWanNetworkSrvModule\":{{\"properties.desired\":{{\"FacadeServerUrl\":\"{facadeURL}\",\"FacadeAuthCode\":\"{facadeAuthCode}\",\"hostAddress\":\"{lnsHostAddress}\",\"network\":\"{NetworkName}\"}}}}}},\"moduleContent\":{{}},\"deviceContent\":{{}}}}";
            Assert.Equal(expectedConfigurationJson, actualConfigurationJson);
        }

        [Fact]
        public async Task DeployEdgeDeviceSettingLogAnalyticsWorkspaceShouldDeployIotHubMetricsCollectorModule()
        {
            // Arrange
            const string logAnalyticsWorkspaceId = "fake-workspace-id";
            const string iothubResourceId = "fake-hub-id";
            const string logAnalyticsWorkspaceKey = "fake-workspace-key";
            const string deviceId = "myGateway";
            const string facadeURL = "https://myfunc.azurewebsites.com/api";
            const string facadeAuthCode = "secret-code";
            const string lnsHostAddress = "ws://mylns:5000";
            const int resetPin = 2;

            Configuration? actualConfiguration = null;
            this.registryManager.Setup(x => x.AddConfigurationAsync(It.IsNotNull<Configuration>()))
                .Callback((Configuration c) => actualConfiguration = c);

            this.registryManager.Setup(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == deviceId && d.Capabilities.IotEdge), It.IsNotNull<Twin>()))
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = true
                });

            // Act
            var args = CreateArgs($"add-gateway --reset-pin {resetPin} --device-id {deviceId} --api-url {facadeURL} --api-key {facadeAuthCode} --lns-host-address {lnsHostAddress} --network {NetworkName} --monitoring true --iothub-resource-id {iothubResourceId} --log-analytics-workspace-id {logAnalyticsWorkspaceId} --log-analytics-shared-key {logAnalyticsWorkspaceKey} --lora-version {LoRaVersion}");
            var actual = await Program.Run(args, this.configurationHelper);

            // Assert
            Assert.Equal(0, actual);
            this.registryManager.Verify(x => x.AddConfigurationAsync(It.IsNotNull<Configuration>()), Times.Once);
            this.registryManager.Verify(c => c.AddDeviceWithTwinAsync(It.Is<Device>(d => d.Id == deviceId && d.Capabilities.IotEdge), It.IsNotNull<Twin>()), Times.Once);

            Assert.NotNull(actualConfiguration);
            Assert.Equal($"deviceId='{deviceId}'", actualConfiguration!.TargetCondition);
            var actualConfigurationJson = JsonConvert.SerializeObject(actualConfiguration.Content);
            var expectedConfigurationJson = $"{{\"modulesContent\":{{\"$edgeAgent\":{{\"properties.desired.modules.IotHubMetricsCollectorModule\":{{\"settings\":{{\"image\":\"mcr.microsoft.com/azureiotedge-metrics-collector:1.0\"}},\"type\":\"docker\",\"env\":{{\"ResourceId\":{{\"value\":\"{iothubResourceId}\"}},\"UploadTarget\":{{\"value\":\"AzureMonitor\"}},\"LogAnalyticsWorkspaceId\":{{\"value\":\"{logAnalyticsWorkspaceId}\"}},\"LogAnalyticsSharedKey\":{{\"value\":\"{logAnalyticsWorkspaceKey}\"}},\"MetricsEndpointsCSV\":{{\"value\":\"http://edgeHub:9600/metrics,http://edgeAgent:9600/metrics\"}}}},\"status\":\"running\",\"restartPolicy\":\"always\",\"version\":\"1.0\"}}}}}},\"moduleContent\":{{}},\"deviceContent\":{{}}}}";
            Assert.Equal(expectedConfigurationJson, actualConfigurationJson);
        }

        [Theory]
        [InlineData("EU863")]
        [InlineData("US902")]
        [InlineData("EU863", "fakeNetwork")]
        [InlineData("US902", "fakeNetwork")]
        public async Task DeployConcentrator(string region, string networkId = NetworkName)
        {
            // Arrange
            const string stationEui = "123456789";
            var eTag = Guid.NewGuid().ToString();
            Twin? emptyTwin = null;
            Twin? savedTwin = null;

            this.registryManager.Setup(c => c.AddDeviceWithTwinAsync(
                    It.Is<Device>(d => d.Id == stationEui),
                    It.IsNotNull<Twin>()))
                .Callback((Device d, Twin t) =>
                {
                    Assert.Equal(networkId, t.Tags[DeviceTags.NetworkTagName].ToString());
                    Assert.Equal(new string[] { DeviceTags.DeviceTypes.Concentrator }, ((JArray)t.Tags[DeviceTags.DeviceTypeTagName]).Select(x => x.ToString()).ToArray());
#pragma warning disable CA1308 // Normalize strings to uppercase
                    Assert.Equal(region.ToLowerInvariant(), t.Tags[DeviceTags.RegionTagName].ToString());
#pragma warning restore CA1308 // Normalize strings to uppercase
                    savedTwin = t;
                })
                .ReturnsAsync(new BulkRegistryOperationResult
                {
                    IsSuccessful = true
                });

            this.registryManager.SetupSequence(c => c.GetTwinAsync(It.Is(stationEui, StringComparer.OrdinalIgnoreCase)))
                // First time it won't find it
                .ReturnsAsync(emptyTwin)
                // After it was inserted we will find it
                .ReturnsAsync(new Twin(stationEui)
                {
                    ETag = eTag
                });

            // Act
            var args = CreateArgs($"add --type concentrator --region {region} --stationeui {stationEui}  --no-cups --network {networkId}");
            var actual = await Program.Run(args, this.configurationHelper);

            // Assert
            Assert.Equal(0, actual);
            Assert.NotNull(savedTwin);

            // Create a JSON without whitespaces or newlines that is easy to compare
            var expectedConf = GetConcentratorRouterConfig(region);
            var expectedRouterConfig = JsonConvert.SerializeObject(expectedConf[TwinProperty.RouterConfig]);

            var actualRouterConfig = JsonUtil.Strictify(savedTwin!.Properties.Desired[TwinProperty.RouterConfig].ToString());
            Assert.Equal(expectedRouterConfig, actualRouterConfig);
        }

        [Fact]
        public async Task DeployConcentratorWithNotImplementedRegionShouldThrowSwitchExpressionException()
        {
            // Act
            var args = CreateArgs($"add --type concentrator --region INVALID --stationeui 1111222  --no-cups --network {NetworkName}");
            var actual = await Program.Run(args, this.configurationHelper);

            // Assert
            Assert.Equal(-1, actual);
        }
    }
}
