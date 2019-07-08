// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
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
            string devEUI = NewUniqueEUI64();
            string devEUI2 = NewUniqueEUI64();
            string gatewayId = NewUniqueEUI64();

            var deviceGetter = new DeviceGetter(this.InitRegistryManager(devEUI, devEUI2), new LoRaInMemoryDeviceStore());
            var items = await deviceGetter.GetDeviceList(devEUI, gatewayId, "ABCD", null);

            Assert.Single(items);
            Assert.Equal(devEUI, items[0].DevEUI);
        }

        private RegistryManager InitRegistryManager(string devEui1, string devEui2)
        {
            var mockRegistryManager = new Mock<RegistryManager>(MockBehavior.Strict);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            mockRegistryManager
                .Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                .ReturnsAsync((string deviceId) => new Device(deviceId) { Authentication = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = primaryKey } } });

            mockRegistryManager
                .Setup(x => x.GetTwinAsync(It.IsNotNull<string>()))
                .ReturnsAsync((string deviceId) => new Twin(deviceId));

            const int numberOfDevices = 2;
            int deviceCount = 0;

            var queryMock = new Mock<IQuery>(MockBehavior.Loose);
            queryMock
                .Setup(x => x.HasMoreResults)
                .Returns(() => (deviceCount < numberOfDevices));

            string[] deviceIds = new string[numberOfDevices] { devEui1, devEui2 };

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
