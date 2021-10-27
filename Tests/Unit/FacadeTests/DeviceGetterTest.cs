// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade;
    using LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Primitives;
    using Moq;
    using Xunit;

    public class DeviceGetterTest : FunctionTestBase
    {
        private const string PrimaryKey = "ABCDEFGH1234567890";
        private readonly Mock<RegistryManager> _registryManagerMock;
        private readonly DeviceGetter _sut;

        public DeviceGetterTest()
        {
            this._registryManagerMock = new Mock<RegistryManager>(MockBehavior.Strict);
            this._sut = new DeviceGetter(this._registryManagerMock.Object, new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public async Task DeviceGetter_OTAA_Join()
        {
            var devEUI = NewUniqueEUI64();
            var devEUI2 = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();
            InitRegistryManager(devEUI, devEUI2);

            var items = await this._sut.GetDeviceList(devEUI, gatewayId, "ABCD", null);

            Assert.Single(items);
            Assert.Equal(devEUI, items[0].DevEUI);
        }

        [Fact]
        public async Task DeviceGetter_DeviceId_Success()
        {
            var deviceId = NewUniqueEUI64();
            SetupGetDeviceAsync(PrimaryKey);

            var httpRequest = HttpRequestHelper.CreateRequest(headers: new Dictionary<string, StringValues> { [ApiVersion.HttpHeaderName] = ApiVersion.LatestVersion.Name },
                                                              queryParameters: new Dictionary<string, StringValues> { ["DeviceId"] = deviceId });

            var items = await this._sut.GetDevice(httpRequest, NullLogger.Instance);
            var result = (IEnumerable<IoTHubDeviceInfo>)((OkObjectResult)items).Value;

            var res = Assert.Single(result);
            Assert.Equal(deviceId, res.DevEUI);
            Assert.Equal(PrimaryKey, Encoding.UTF8.GetString(Convert.FromBase64String(res.PrimaryKey)));
            this._registryManagerMock.VerifyAll();
        }

        [Fact]
        public async Task DeviceGetter_DeviceId_Not_Found()
        {
            var deviceId = NewUniqueEUI64();
            _ = this._registryManagerMock.Setup(rm => rm.GetDeviceAsync(It.IsAny<string>()))
                                         .Returns(Task.FromResult((Device)null));

            var httpRequest = HttpRequestHelper.CreateRequest(headers: new Dictionary<string, StringValues> { [ApiVersion.HttpHeaderName] = ApiVersion.LatestVersion.Name },
                                                              queryParameters: new Dictionary<string, StringValues> { ["DeviceId"] = deviceId });

            var result = await this._sut.GetDevice(httpRequest, NullLogger.Instance);

            _ = Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeviceGetter_Returns_Bad_Request_When_No_Query_Params_Are_Set()
        {
            var httpRequest = HttpRequestHelper.CreateRequest(headers: new Dictionary<string, StringValues> { [ApiVersion.HttpHeaderName] = ApiVersion.LatestVersion.Name });

            var result = await this._sut.GetDevice(httpRequest, NullLogger.Instance);

            var res = Assert.IsType<BadRequestObjectResult>(result);
            Assert.True(((string)res.Value).Contains("no query parameters", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task DeviceGetter_Returns_Bad_Request_When_No_Api_Set()
        {
            var httpRequest = HttpRequestHelper.CreateRequest();

            var result = await this._sut.GetDevice(httpRequest, NullLogger.Instance);

            var res = Assert.IsType<BadRequestObjectResult>(result);
            Assert.True(((string)res.Value).Contains("version", StringComparison.OrdinalIgnoreCase));
        }

        private void InitRegistryManager(string devEui1, string devEui2)
        {
            SetupGetDeviceAsync(PrimaryKey);

            _registryManagerMock
                .Setup(x => x.GetTwinAsync(It.IsNotNull<string>()))
                .ReturnsAsync((string deviceId) => new Twin(deviceId));

            const int numberOfDevices = 2;
            var deviceCount = 0;

            var queryMock = new Mock<IQuery>(MockBehavior.Loose);
            queryMock
                .Setup(x => x.HasMoreResults)
                .Returns(() => deviceCount < numberOfDevices);

            var deviceIds = new string[numberOfDevices] { devEui1, devEui2 };

            IEnumerable<Twin> Twins()
            {
                while (deviceCount < numberOfDevices)
                {
                    yield return new Twin(deviceIds[deviceCount++]);
                }
            }

            queryMock
                .Setup(x => x.GetNextAsTwinAsync())
                .ReturnsAsync(Twins());

            _registryManagerMock
                .Setup(x => x.CreateQuery(It.IsAny<string>(), 100))
                .Returns(queryMock.Object);

            _registryManagerMock
                .Setup(x => x.CreateQuery(It.IsAny<string>()))
                .Returns(queryMock.Object);
        }

        private void SetupGetDeviceAsync(string primaryKey) =>
            _registryManagerMock.Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                                .ReturnsAsync((string deviceId) => new Device(deviceId)
                                {
                                    Authentication = new AuthenticationMechanism
                                    {
                                        SymmetricKey = new SymmetricKey
                                        {
                                            PrimaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(primaryKey))
                                        }
                                    }
                                });
    }
}
