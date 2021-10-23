// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoraKeysManagerFacade;
    using LoRaWan.Tests.Common;
    using Moq;
    using Xunit;

    public class DeviceGetterTest : FunctionTestBase
    {
        private const string PrimaryKey = "ABCDEFGH1234567890";
        protected const string IotHubHostName = "fake.azure-devices.net";

        [Fact]
        public async void DeviceGetter_OTAA_Join()
        {
            var devEUI = NewUniqueEUI64();
            var devEUI2 = NewUniqueEUI64();
            var gatewayId = NewUniqueEUI64();

            var deviceGetter = new DeviceGetter(InitRegistryManager(devEUI, devEUI2), new LoRaInMemoryDeviceStore());
            var items = await deviceGetter.GetDeviceList(devEUI, gatewayId, "ABCD", null);

            Assert.Single(items);
            Assert.Equal(devEUI, items[0].DevEUI);
        }

        private static IDeviceRegistryManager InitRegistryManager(string devEui1, string devEui2)
        {
            var mockRegistryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));

            mockRegistryManager
                .Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                .ReturnsAsync((string deviceId) =>
                {
                    var mockDevice = new Mock<IDevice>(MockBehavior.Strict);

                    mockDevice.SetupGet(t => t.PrimaryKey)
                        .Returns(primaryKey);
                    mockDevice.SetupGet(t => t.AssignedIoTHub)
                            .Returns(IotHubHostName);

                    return mockDevice.Object;
                });

            mockRegistryManager
                .Setup(x => x.GetTwinAsync(It.IsNotNull<string>()))
                .ReturnsAsync((string deviceId) =>
                {
                    var mockDevice = new Mock<IDeviceTwin>(MockBehavior.Strict);

                    mockDevice.SetupGet(t => t.DeviceId)
                        .Returns(deviceId);
                    mockDevice.Setup(t => t.GetDevAddr())
                              .Returns(string.Empty);
                    mockDevice.Setup(t => t.GetGatewayID())
                              .Returns(string.Empty);
                    mockDevice.Setup(t => t.GetLastUpdated())
                              .Returns(DateTime.UtcNow);

                    return mockDevice.Object;
                });

            const int numberOfDevices = 2;
            var deviceCount = 0;

            var queryMock = new Mock<IRegistryPageResult<IDeviceTwin>>(MockBehavior.Loose);
            queryMock
                .Setup(x => x.HasMoreResults)
                .Returns(() => deviceCount < numberOfDevices);

            var deviceIds = new string[numberOfDevices] { devEui1, devEui2 };

            IEnumerable<IDeviceTwin> Twins()
            {
                while (deviceCount < numberOfDevices)
                {
                    var mockDevice = new Mock<IDeviceTwin>(MockBehavior.Strict);

                    mockDevice.SetupGet(t => t.DeviceId)
                        .Returns(deviceIds[deviceCount++]);
                    mockDevice.Setup(t => t.GetDevAddr())
                              .Returns(string.Empty);
                    mockDevice.Setup(t => t.GetGatewayID())
                              .Returns(string.Empty);
                    mockDevice.Setup(t => t.GetLastUpdated())
                              .Returns(DateTime.UtcNow);

                    yield return mockDevice.Object;
                }
            }

            queryMock
                .Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(Twins());

            mockRegistryManager
                .Setup(x => x.FindDeviceByAddrAsync(It.IsAny<string>()))
                .ReturnsAsync(queryMock.Object);

            return mockRegistryManager.Object;
        }
    }
}
