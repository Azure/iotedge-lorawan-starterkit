// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Formatting;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp;
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
            var device = new IoTCentralImp.Definitions.Device
            {
                Id = NewUniqueEUI32(),
                Provisionned = true
            };
            IoTCentralImp.Definitions.SymmetricKeyAttestation attestation = null;

            var bytes = new byte[64];
            var random = new Random();
            random.NextBytes(bytes);

            using (var hmac = new HMACSHA256(bytes))
            {
                attestation = new IoTCentralImp.Definitions.SymmetricKeyAttestation
                {
                    Type = "symmetricKey",
                    SymmetricKey = new IoTCentralImp.Definitions.SymmetricKey
                    {
                        PrimaryKey = Convert.ToBase64String(hmac.ComputeHash(bytes)),
                        SecondaryKey = Convert.ToBase64String(hmac.ComputeHash(bytes))
                    }
                };
            }

            deviceResponseMock.Content = new StringContent(JsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            var attestationResponseMock = new HttpResponseMessage();
            attestationResponseMock.Content = new StringContent(JsonSerializer.Serialize(attestation), Encoding.UTF8, "application/json");

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}"))
                    {
                        return deviceResponseMock;
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}/attestation"))
                    {
                        return attestationResponseMock;
                    }

                    return null;
                })
                .Verifiable();

            var mockHttpClient = this.InitHttpClient(handlerMock);

            IoTCentralDeviceRegistryManager instance = new IoTCentralDeviceRegistryManager(provisioningHelperMock.Object, mockHttpClient);

            var response = await instance.GetDeviceAsync(device.Id);

            Assert.NotNull(response);
            Assert.Equal(device.Id, response.DeviceId);
            Assert.Equal(attestation.SymmetricKey.PrimaryKey, response.PrimaryKey);

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(2), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        [Fact]
        // This test ensure that IoT Central implementation of DeviceRegistry calls the IoT Hub with correct parameters and returns the correspondig answer
        public async Task Get_Device_NotProvisionned_Should_Be_Provisionned()
        {
            var provisioningHelperMock = new Mock<IDeviceProvisioningHelper>(MockBehavior.Strict);
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            var expectedPrimaryKey = string.Empty;

            var deviceResponseMock = new HttpResponseMessage();
            var device = new IoTCentralImp.Definitions.Device
            {
                Id = NewUniqueEUI32(),
                Provisionned = false
            };
            IoTCentralImp.Definitions.SymmetricKeyAttestation attestation = null;

            deviceResponseMock.Content = new StringContent(JsonSerializer.Serialize(device), Encoding.UTF8, "application/json");

            var notFoundResponseMock = new HttpResponseMessage(HttpStatusCode.NotFound);

            bool attestationPosted = false;

            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync((HttpRequestMessage req, CancellationToken token) =>
                {
                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}"))
                    {
                        return deviceResponseMock;
                    }

                    if (req.RequestUri.LocalPath.Equals($"/api/devices/{device.Id}/attestation") && req.Method == HttpMethod.Put)
                    {
                        attestationPosted = true;

                        var response = new HttpResponseMessage(HttpStatusCode.OK);
                        response.Content = req.Content;
                        attestation = req.Content.ReadAsAsync<IoTCentralImp.Definitions.SymmetricKeyAttestation>().Result;

                        return response;
                    }

                    return null;
                })
                .Verifiable();

            provisioningHelperMock.Setup(c => c.ProvisionDeviceAsync(It.IsAny<string>(), It.IsAny<IoTCentralImp.Definitions.SymmetricKeyAttestation>()))
                       .ReturnsAsync((string deviceId, IoTCentralImp.Definitions.SymmetricKeyAttestation att) => true);
            var mockHttpClient = this.InitHttpClient(handlerMock);

            IoTCentralDeviceRegistryManager instance = new IoTCentralDeviceRegistryManager(provisioningHelperMock.Object, mockHttpClient);

            var response = await instance.GetDeviceAsync(device.Id);

            Assert.NotNull(response);
            Assert.Equal(device.Id, response.DeviceId);
            Assert.Equal(attestation.SymmetricKey.PrimaryKey, response.PrimaryKey);
            Assert.True(attestationPosted);

            handlerMock.Protected().Verify("SendAsync", Times.Exactly(2), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
            provisioningHelperMock.Verify(c => c.ProvisionDeviceAsync(It.Is<string>(id => device.Id == id), It.Is<IoTCentralImp.Definitions.SymmetricKeyAttestation>(a => a == attestation)), Times.Once());
        }
    }
}
