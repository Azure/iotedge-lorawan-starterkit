// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoraKeysManagerFacade.IoTCentralImp;
    using global::LoraKeysManagerFacade.IoTCentralImp.Definitions;
    using LoRaWan.Tests.Common;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class IoTCentralRegistryManagerTest : FunctionTestBase
    {
        private static DevAddr CreateDevAddr() => new DevAddr((uint)RandomNumberGenerator.GetInt32(int.MaxValue));

        private static HttpClient InitHttpClient(Mock<HttpMessageHandler> handlerMock)
        {
            return new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost.local/")
            };
        }

        [Fact]
        // This test ensure that IoT Central implementation of DeviceRegistry calls the IoT Hub with correct parameters and returns the correspondig answer
        public async Task Get_Device_Call_IoTCentral()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            using var deviceResponseMock = new HttpResponseMessage();

            var device = new Device
            {
                Id = NewUniqueEUI32(),
                Provisioned = true
            };

            var attestation = new SymmetricKeyAttestation
            {
                Type = "symmetricKey",
                SymmetricKey = new SymmetricKey
                {
                    PrimaryKey = Guid.NewGuid().ToString(),
                    SecondaryKey = Guid.NewGuid().ToString()
                }
            };

            provisioningHelperMock.Setup(x => x.ComputeAttestation(It.IsAny<string>()))
                                .Returns(attestation);

            provisioningHelperMock.Setup(c => c.ProvisionDevice(It.IsAny<string>()))
                            .ReturnsAsync(new DeviceProvisioningResult
                            {
                                AssignedIoTHubHostname = string.Empty,
                                Attestation = attestation
                            });

            deviceResponseMock.Content = new StringContent(JsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            handlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}", StringComparison.OrdinalIgnoreCase))
                    {
                        return deviceResponseMock;
                    }

                    return null;
                })
                .Verifiable();

            using var mockHttpClient = InitHttpClient(handlerMock);

            var instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var response = await instance.GetDeviceAsync(device.Id);

            Assert.NotNull(response);
            Assert.Equal(device.Id, response.DeviceId);
            Assert.Equal(attestation.SymmetricKey.PrimaryKey, response.PrimaryKey);

            provisioningHelperMock.Verify(c => c.ProvisionDevice(It.Is<string>(id => device.Id == id)), Times.Once());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{device.Id}"), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        // This test ensure that IoT Central implementation of DeviceRegistry calls the IoT Hub with correct parameters and returns the correspondig answer
        public async Task Get_Device_NotProvisionned_Should_Be_Provisionned()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            using var deviceResponseMock = new HttpResponseMessage();

            var device = new Device
            {
                Id = NewUniqueEUI32(),
                Provisioned = false
            };

            deviceResponseMock.Content = new StringContent(JsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            handlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}", StringComparison.OrdinalIgnoreCase))
                    {
                        return deviceResponseMock;
                    }

                    return null;
                })
                .Verifiable();

            provisioningHelperMock.Setup(c => c.ProvisionDevice(It.IsAny<string>()))
                            .ReturnsAsync(new DeviceProvisioningResult
                            {
                                AssignedIoTHubHostname = string.Empty,
                                Attestation = new SymmetricKeyAttestation()
                            });

            using var mockHttpClient = InitHttpClient(handlerMock);

            var instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var response = await instance.GetDeviceAsync(device.Id);

            Assert.NotNull(response);
            Assert.Equal(device.Id, response.DeviceId);

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{device.Id}"), ItExpr.IsAny<CancellationToken>());

            provisioningHelperMock.Verify(c => c.ProvisionDevice(It.Is<string>(id => device.Id == id)), Times.Once());
        }

        [Fact]
        public async Task Get_Twin()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var device = new Device
            {
                Id = NewUniqueEUI32(),
                Provisioned = false
            };

            using var devicePropertiesContent = new StringContent(
                    "{" +
                    "    \"AppEUI\": \"trsr\"," +
                    "    \"$metadata\": {" +
                    "        \"GatewayID\": {" +
                    "            \"desiredValue\": \"trsr\"," +
                    "            \"desiredVersion\": 2," +
                    "            \"lastUpdateTime\": \"2021-09-19T14:53:48.4195048Z\"," +
                    "            \"ackVersion\": 2," +
                    "            \"ackDescription\": \"completed\"," +
                    "            \"ackCode\": 200" +
                    "                }," +
                    "        \"DevAddr\": {" +
                    "            \"desiredValue\": \"jjh\"," +
                    "            \"desiredVersion\": 2," +
                    "            \"lastUpdateTime\": \"2021-09-19T14:53:48.4195048Z\"," +
                    "            \"ackVersion\": 2," +
                    "            \"ackDescription\": \"completed\"," +
                    "            \"ackCode\": 200" +
                    "        }," +
                    "        \"NwkSKey\": {" +
                    "            \"desiredValue\": \"SF11BW500\"," +
                    "            \"desiredVersion\": 2," +
                    "            \"lastUpdateTime\": \"2021-09-19T14:53:48.4195048Z\"," +
                    "            \"ackVersion\": 2," +
                    "            \"ackDescription\": \"completed\"," +
                    "            \"ackCode\": 200" +
                    "        }" +
                    "    }" +
                    "}",
                    Encoding.UTF8,
                    "application/json");

            handlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}/properties", StringComparison.OrdinalIgnoreCase))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = devicePropertiesContent
                        };
                    }

                    return null;
                })
                .Verifiable();

            using var mockHttpClient = InitHttpClient(handlerMock);

            var instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var properties = await instance.GetTwinAsync(device.Id);

            Assert.NotNull(properties);
            Assert.NotEqual(DateTime.MinValue, properties.LastUpdated);

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{device.Id}/properties"), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task Find_Configured_LoRa_Devices()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var modelDefinitionId = string.Empty;
            var componentName = string.Empty;

            handlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals("/api/deviceTemplates", StringComparison.OrdinalIgnoreCase))
                    {
                        return GenerateDeviceTemplateResponse(out modelDefinitionId, out componentName);
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/query", StringComparison.OrdinalIgnoreCase))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                            $"{{" +
                            $"    \"results\": [" +
                            $"        {{" +
                            $"              \"$id\":  \"vn1lwbzhic\"," +
                            $"              \"{componentName}.DevAddr\":  \"{CreateDevAddr()}\"," +
                            $"              \"{componentName}.NwkSKey\":  \"{NewUniqueEUI64()}\"," +
                            $"              \"{componentName}.GatewayID\":  \"{NewUniqueEUI64()}\"" +
                            $"         }}" +
                            $"     ]",
                            Encoding.UTF8,
                            "application/json")
                        };
                    }

                    return null;
                })
                .Verifiable();

            using var mockHttpClient = InitHttpClient(handlerMock);

            var instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var results = await instance.FindConfiguredLoRaDevices();

            Assert.NotNull(results);
            Assert.True(results.HasMoreResults);
            var page = await results.GetNextPageAsync();
            Assert.NotNull(page);
            Assert.False(results.HasMoreResults);
            Assert.Single(page);
            Assert.Single(page, c => c.DeviceId == "vn1lwbzhic");

            handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/deviceTemplates"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(c =>
                    c.Method == HttpMethod.Post &&
                    c.RequestUri.LocalPath == $"/api/query" &&
                    string.Equals(c.Content.ReadAsStringAsync().Result,
                         $"{{\"query\":\"SELECT $id, {componentName}.DevAddr, {componentName}.NwkSKey, {componentName}.GatewayID FROM {modelDefinitionId} WHERE {componentName}.AppKey != \\\"\\\" AND {componentName}.AppSKey != \\\"\\\" AND {componentName}.NwkSKey != \\\"\\\" AND $simulated = false\"}}",
                        StringComparison.OrdinalIgnoreCase)),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task Find_Device_By_DevAddr()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var modelDefinitionId = string.Empty;
            var componentName = string.Empty;

            var devAddr = CreateDevAddr();

            handlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals("/api/deviceTemplates", StringComparison.OrdinalIgnoreCase))
                    {
                        return GenerateDeviceTemplateResponse(out modelDefinitionId, out componentName);
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/query", StringComparison.OrdinalIgnoreCase))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                            $"{{" +
                            $"    \"results\": [" +
                            $"        {{" +
                            $"              \"$id\":  \"vn1lwbzhic\"," +
                            $"              \"{componentName}.DevAddr\":  \"{devAddr}\"," +
                            $"              \"{componentName}.NwkSKey\":  \"{NewUniqueEUI64()}\"," +
                            $"              \"{componentName}.GatewayID\":  \"{NewUniqueEUI64()}\"" +
                            $"         }}" +
                            $"     ]",
                            Encoding.UTF8,
                            "application/json")
                        };
                    }

                    return null;
                })
                .Verifiable();

            using var mockHttpClient = InitHttpClient(handlerMock);

            var instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var results = await instance.FindDeviceByAddrAsync(devAddr);

            Assert.NotNull(results);
            Assert.True(results.HasMoreResults);
            var page = await results.GetNextPageAsync();
            Assert.NotNull(page);
            Assert.False(results.HasMoreResults);
            Assert.Single(page);
            Assert.Single(page, c => c.DeviceId == "vn1lwbzhic");

            handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/deviceTemplates"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(c =>
                    c.Method == HttpMethod.Post &&
                    c.RequestUri.LocalPath == $"/api/query" &&
                    string.Equals(c.Content.ReadAsStringAsync().Result,
                         $"{{\"query\":\"SELECT $id, {componentName}.DevAddr, {componentName}.NwkSKey, {componentName}.GatewayID FROM {modelDefinitionId} WHERE {componentName}.DevAddr = \\\"{devAddr}\\\" AND $simulated = false\"}}",
                        StringComparison.OrdinalIgnoreCase)),
                ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task Find_Devices_By_Last_UpdateDate()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var modelDefinitionId = string.Empty;
            var componentName = string.Empty;
            var devAddr = NewUniqueEUI32();

            handlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals("/api/deviceTemplates", StringComparison.OrdinalIgnoreCase))
                    {
                        return GenerateDeviceTemplateResponse(out modelDefinitionId, out componentName);
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/query", StringComparison.OrdinalIgnoreCase))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                            $"{{" +
                            $"    \"results\": [" +
                            $"        {{" +
                            $"              \"$id\":  \"vn1lwbzhic\"," +
                            $"              \"{componentName}.DevAddr\":  \"{devAddr}\"," +
                            $"              \"{componentName}.NwkSKey\":  \"{NewUniqueEUI64()}\"," +
                            $"              \"{componentName}.GatewayID\":  \"{NewUniqueEUI64()}\"" +
                            $"         }}" +
                            $"     ]",
                            Encoding.UTF8,
                            "application/json")
                        };
                    }

                    return null;
                })
                .Verifiable();

            using var mockHttpClient = InitHttpClient(handlerMock);

            var instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var results = await instance.FindDevicesByLastUpdateDate(DateTime.Now.AddMinutes(-5).ToString("o"));

            Assert.NotNull(results);
            Assert.True(results.HasMoreResults);
            var page = await results.GetNextPageAsync();
            Assert.NotNull(page);
            Assert.False(results.HasMoreResults);
            Assert.Single(page);
            Assert.Single(page, c => c.DeviceId == "vn1lwbzhic");

            handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/deviceTemplates"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(c =>
                    c.Method == HttpMethod.Post &&
                    c.RequestUri.LocalPath == $"/api/query" &&
                    string.Equals(c.Content.ReadAsStringAsync().Result,
                         $"{{\"query\":\"SELECT $id, {componentName}.DevAddr, {componentName}.NwkSKey, {componentName}.GatewayID FROM {modelDefinitionId} WHERE {componentName}.AppKey != \\\"\\\" AND {componentName}.AppSKey != \\\"\\\" AND {componentName}.NwkSKey != \\\"\\\" AND $simulated = false\"}}",
                        StringComparison.OrdinalIgnoreCase)),
                ItExpr.IsAny<CancellationToken>());
        }


        [Fact]
        public async Task When_Getting_A_Device_That_Not_Exists_Not_Provisioned()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            using var deviceResponseMock = new HttpResponseMessage();

            var device = new Device
            {
                Id = NewUniqueEUI32(),
                Provisioned = false
            };

            deviceResponseMock.Content = new StringContent(JsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            handlerMock.Protected()
                .Setup("Dispose", ItExpr.IsAny<bool>());

            using (var notFoundResponseMock = new HttpResponseMessage(HttpStatusCode.NotFound))
            {
                handlerMock
                    .Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                    {
                        if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}", StringComparison.OrdinalIgnoreCase))
                        {
                            return notFoundResponseMock;
                        }

                        return null;
                    })
                    .Verifiable();
            }

            using var mockHttpClient = InitHttpClient(handlerMock);

            var instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var response = await instance.GetDeviceAsync(device.Id);

            Assert.Null(response);

            handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{device.Id}"), ItExpr.IsAny<CancellationToken>());

            provisioningHelperMock.Verify(c => c.ProvisionDevice(It.Is<string>(id => device.Id == id)), Times.Never());
        }

        private static HttpResponseMessage GenerateDeviceTemplateResponse(out string modelDefinitionId, out string componentName)
        {
            modelDefinitionId = Guid.NewGuid().ToString();
            componentName = Guid.NewGuid().ToString();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"{{" +
                    $"    \"value\": [" +
                    $"        {{" +
                    $"            \"displayName\": \"SampleLoRa\", " +
                    $"            \"capabilityModel\": {{" +
                    $"                  \"@id\": \"dtmi:gddpIotcentral:SampleLoRa5fv;1\"," +
                    $"                  \"@type\": \"Interface\", " +
                    $"                  \"contents\": [ " +
                    $"                      {{" +
                    $"                          \"@id\": \"dtmi:gddpIotcentral:SampleLoRa5fv:LoRa;1\"," +
                    $"                          \"@type\": \"Component\"," +
                    $"                          \"displayName\": \"LoRa\"," +
                    $"                          \"name\": \"{componentName}\"," +
                    $"                          \"schema\": {{" +
                    $"                                \"@id\": \"dtmi:iotcentral:LoRaDevice;1\", " +
                    $"                                \"@type\": \"Interface\"," +
                    $"                                \"contents\": [" +
                    $"                                  ]" +
                    $"                          }}" +
                    $"                      }}" +
                    $"                  ]" +
                    $"              }}," +
                    $"              \"@id\": \"{modelDefinitionId}\"," +
                    $"              \"@type\": [" +
                    $"                  \"ModelDefinition\"," +
                    $"                  \"DeviceModel\"" +
                    $"              ], " +
                    $"              \"@context\": [" +
                    $"                  \"dtmi:iotcentral:context;2\"," +
                    $"                  \"dtmi:dtdl:context;2\"" +
                    $"              ]" +
                    $"          }}" +
                    $"     ]" +
                    $"}}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
