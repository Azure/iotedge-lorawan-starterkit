// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;
    using Moq;
    using Moq.Protected;
    using Xunit;

    public class IoTCentralRegistryManagerTest : FunctionTestBase
    {
        private HttpClient InitHttpClient(Mock<HttpMessageHandler> handlerMock)
        {
            return new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://localhost.local/")
            };
        }

        [Fact]
        // This test ensure that IoT Central implementation of DeviceRegistry calls the IoT Hub with correct parameters and returns the correspondig answer
        public async Task Get_Device_Call_IoTHub()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var expectedPrimaryKey = string.Empty;

            var deviceResponseMock = new HttpResponseMessage();
            var device = new Device
            {
                Id = NewUniqueEUI32(),
                Provisioned = true
            };

            SymmetricKeyAttestation attestation = new SymmetricKeyAttestation
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

            deviceResponseMock.Content = new StringContent(JsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}"))
                    {
                        return deviceResponseMock;
                    }

                    return null;
                })
                .Verifiable();

            var mockHttpClient = this.InitHttpClient(handlerMock);

            IoTCentralDeviceRegistryManager instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var response = await instance.GetDeviceAsync(device.Id);

            Assert.NotNull(response);
            Assert.Equal(device.Id, response.DeviceId);
            Assert.Equal(attestation.SymmetricKey.PrimaryKey, response.PrimaryKey);

            provisioningHelperMock.Verify(x => x.ComputeAttestation(It.Is<string>(c => c == device.Id)), Times.Once());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{device.Id}"), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        // This test ensure that IoT Central implementation of DeviceRegistry calls the IoT Hub with correct parameters and returns the correspondig answer
        public async Task Get_Device_NotProvisionned_Should_Be_Provisionned()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var expectedPrimaryKey = string.Empty;

            var deviceResponseMock = new HttpResponseMessage();
            var device = new Device
            {
                Id = NewUniqueEUI32(),
                Provisioned = false
            };

            deviceResponseMock.Content = new StringContent(JsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            var notFoundResponseMock = new HttpResponseMessage(HttpStatusCode.NotFound);

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}"))
                    {
                        return deviceResponseMock;
                    }

                    return null;
                })
                .Verifiable();

            provisioningHelperMock.Setup(c => c.ProvisionDeviceAsync(It.IsAny<string>()))
                       .ReturnsAsync((string deviceId) => new SymmetricKeyAttestation());
            var mockHttpClient = this.InitHttpClient(handlerMock);

            IoTCentralDeviceRegistryManager instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var response = await instance.GetDeviceAsync(device.Id);

            Assert.NotNull(response);
            Assert.Equal(device.Id, response.DeviceId);

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{device.Id}"), ItExpr.IsAny<CancellationToken>());

            provisioningHelperMock.Verify(c => c.ProvisionDeviceAsync(It.Is<string>(id => device.Id == id)), Times.Once());
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

            var devicePropertiesContent = new StringContent(
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

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}/properties"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = devicePropertiesContent
                        };
                    }

                    return null;
                })
                .Verifiable();

            var mockHttpClient = this.InitHttpClient(handlerMock);
            IoTCentralDeviceRegistryManager instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var properties = await instance.GetTwinAsync(device.Id);

            Assert.NotNull(properties);
            Assert.NotEqual(DateTime.MinValue, properties.GetLastUpdated());

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{device.Id}/properties"), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task Find_Configured_LoRa_Devices()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var data = new DeviceCollection
            {
                Value = Enumerable.Union(
                    new[]
                    {
                        new Device { Id = "not-lora" },
                        new Device { Id = "simulated", Simulated = true }
                    },
                    Enumerable.Range(0, 3).Select(x => new Device
                    {
                        Id = NewUniqueEUI32()
                    }))
            };

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
                        };
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/devices/not-lora/properties"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(new DesiredProperties()),
                                Encoding.UTF8,
                                "application/json")
                        };
                    }

                    if (req.RequestUri.LocalPath.StartsWith($"/api/devices") &&
                        req.RequestUri.LocalPath.EndsWith($"/properties"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(new DesiredProperties
                                {
                                    DevAddr = NewUniqueEUI64(),
                                    NwkSKey = NewUniqueEUI64()
                                }), Encoding.UTF8,
                                "application/json")
                        };
                    }

                    return null;
                })
                .Verifiable();

            var mockHttpClient = this.InitHttpClient(handlerMock);

            IoTCentralDeviceRegistryManager instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var results = await instance.FindConfiguredLoRaDevices();

            Assert.NotNull(results);
            Assert.True(results.HasMoreResults);
            var page = await results.GetNextPageAsync();
            Assert.NotNull(page);
            Assert.False(results.HasMoreResults);
            Assert.Equal(3, page.Count());

            Assert.True(!page.Any(c => c.DeviceId == "simulated"));
            Assert.True(!page.Any(c => c.DeviceId == "not-lora"));

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/simulated/properties"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/not-lora/properties"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(4), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath.StartsWith($"/api/devices") && c.RequestUri.LocalPath.EndsWith("/properties")), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task Find_Device_By_DevAddr()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var devAddr = NewUniqueEUI32();

            var data = new DeviceCollection
            {
                Value = Enumerable.Union(
                    new[]
                    {
                        new Device { Id = "not-lora" },
                        new Device { Id = "simulated", Simulated = true },
                        new Device { Id = devAddr }
                    },
                    Enumerable.Range(0, 3).Select(x => new Device
                    {
                        Id = NewUniqueEUI32()
                    }))
            };

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
                        };
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/devices/not-lora/properties"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(new DesiredProperties()),
                                Encoding.UTF8,
                                "application/json")
                        };
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{devAddr}/properties"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(new DesiredProperties()
                                {
                                    DevAddr = devAddr
                                }),
                                Encoding.UTF8,
                                "application/json")
                        };
                    }

                    if (req.RequestUri.LocalPath.StartsWith($"/api/devices") &&
                        req.RequestUri.LocalPath.EndsWith($"/properties"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(new DesiredProperties
                                {
                                    DevAddr = NewUniqueEUI64(),
                                    NwkSKey = NewUniqueEUI64()
                                }), Encoding.UTF8,
                                "application/json")
                        };
                    }

                    return null;
                })
                .Verifiable();

            var mockHttpClient = this.InitHttpClient(handlerMock);

            IoTCentralDeviceRegistryManager instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var results = await instance.FindDeviceByAddrAsync(devAddr);

            Assert.NotNull(results);
            Assert.True(results.HasMoreResults);
            var page = await results.GetNextPageAsync();
            Assert.NotNull(page);
            Assert.False(results.HasMoreResults);
            Assert.Single(page);

            Assert.True(!page.Any(c => c.DeviceId == "simulated"));
            Assert.True(!page.Any(c => c.DeviceId == "not-lora"));

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{devAddr}/properties"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/simulated/properties"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/not-lora/properties"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(5), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath.StartsWith($"/api/devices") && c.RequestUri.LocalPath.EndsWith("/properties")), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        public async Task Find_Devices_By_Last_UpdateDate()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            var devAddr = NewUniqueEUI32();

            var data = new DeviceCollection
            {
                Value = Enumerable.Union(
                    new[]
                    {
                        new Device { Id = "not-lora" },
                        new Device { Id = "simulated", Simulated = true },
                        new Device { Id = devAddr }
                    },
                    Enumerable.Range(0, 3).Select(x => new Device
                    {
                        Id = NewUniqueEUI32()
                    }))
            };

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json")
                        };
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/devices/not-lora/properties"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(new DesiredProperties()),
                                Encoding.UTF8,
                                "application/json")
                        };
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{devAddr}/properties"))
                    {
                        var devicePropertiesContent = new StringContent(
                            $"{{" +
                            $"    \"AppEUI\": \"trsr\"," +
                            $"    \"$metadata\": {{" +
                            $"        \"GatewayID\": {{" +
                            $"            \"desiredValue\": \"\"," +
                            $"            \"desiredVersion\": 2," +
                            $"            \"lastUpdateTime\": \"{DateTime.Now.AddMinutes(-2).ToString("o")}\"," +
                            $"            \"ackVersion\": 2," +
                            $"            \"ackDescription\": \"completed\"," +
                            $"            \"ackCode\": 200" +
                            $"        }}," +
                            $"        \"DevAddr\": {{" +
                            $"            \"desiredValue\": \"{devAddr}\"," +
                            $"            \"desiredVersion\": 2," +
                            $"            \"lastUpdateTime\": \"{DateTime.Now.AddMinutes(-2).ToString("o")}\"," +
                            $"            \"ackVersion\": 2," +
                            $"            \"ackDescription\": \"completed\"," +
                            $"            \"ackCode\": 200" +
                            $"        }}," +
                            $"        \"NwkSKey\": {{" +
                            $"            \"desiredValue\": \"SF11BW500\"," +
                            $"            \"desiredVersion\": 2," +
                            $"            \"lastUpdateTime\": \"{DateTime.Now.AddMinutes(-2).ToString("o")}\"," +
                            $"            \"ackVersion\": 2," +
                            $"            \"ackDescription\": \"completed\"," +
                            $"            \"ackCode\": 200" +
                            $"        }}" +
                            $"    }}" +
                            $"}}",
                            Encoding.UTF8,
                            "application/json");

                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = devicePropertiesContent
                        };
                    }

                    if (req.RequestUri.LocalPath.StartsWith($"/api/devices") &&
                        req.RequestUri.LocalPath.EndsWith($"/properties"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                JsonSerializer.Serialize(new DesiredProperties
                                {
                                    DevAddr = NewUniqueEUI64(),
                                    NwkSKey = NewUniqueEUI64()
                                }), Encoding.UTF8,
                                "application/json")
                        };
                    }

                    return null;
                })
                .Verifiable();

            var mockHttpClient = this.InitHttpClient(handlerMock);

            IoTCentralDeviceRegistryManager instance = new IoTCentralDeviceRegistryManager(mockHttpClient, provisioningHelperMock.Object);

            var results = await instance.FindDevicesByLastUpdateDate(DateTime.Now.AddMinutes(-5).ToString("o"));

            Assert.NotNull(results);
            Assert.True(results.HasMoreResults);
            var page = await results.GetNextPageAsync();
            Assert.NotNull(page);
            Assert.False(results.HasMoreResults);
            Assert.Single(page);

            Assert.True(!page.Any(c => c.DeviceId == "simulated"));
            Assert.True(!page.Any(c => c.DeviceId == "not-lora"));

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(1), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/{devAddr}/properties"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Never(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/simulated/properties"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Once(), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath == $"/api/devices/not-lora/properties"), ItExpr.IsAny<CancellationToken>());
            handlerMock.Protected().Verify("SendAsync", Times.Exactly(5), ItExpr.Is<HttpRequestMessage>(c => c.Method == HttpMethod.Get && c.RequestUri.LocalPath.StartsWith($"/api/devices") && c.RequestUri.LocalPath.EndsWith("/properties")), ItExpr.IsAny<CancellationToken>());
        }
    }
}
