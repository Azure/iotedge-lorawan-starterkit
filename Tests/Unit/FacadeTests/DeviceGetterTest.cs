// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoraKeysManagerFacade;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class DeviceGetterTest : FunctionTestBase
    {
        private const string PrimaryKey = "ABCDEFGH1234567890";

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

        private IDeviceRegistryManager InitRegistryManager(string devEui1, string devEui2)
        {
            var mockRegistryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            mockRegistryManager
                .Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                .ReturnsAsync((string deviceId) => new Device(deviceId) { Authentication = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = primaryKey } } });

            mockRegistryManager
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

            mockRegistryManager
                .Setup(x => x.CreateQuery(It.IsAny<string>(), 100))
                .Returns(queryMock.Object);

            mockRegistryManager
                .Setup(x => x.CreateQuery(It.IsAny<string>()))
                .Returns(queryMock.Object);

            return mockRegistryManager.Object;
        }
    }
}
