// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.Test
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Azure.WebJobs;
    using Moq;
    using Xunit;

    // Ensure tests don't run in parallel since LoRaRegistryManager is shared
    [Collection("LoraKeysManagerFacade.Test")]
    public class DeviceGetterTest
    {
        private const string PrimaryKey = "ABCDEFGH1234567890";

        private readonly Mock<RegistryManager> mockRegistryManager = new Mock<RegistryManager>(MockBehavior.Loose);

        private readonly ExecutionContext dummyContext = new ExecutionContext();

        public DeviceGetterTest()
        {
            LoRaDeviceCache.EnsureCacheStore(new LoRaInMemoryDeviceStore());
        }

        [Fact]
        public async void DeviceGetter_OTAA_Join()
        {
            const string DevEUI = "1234567890123456";
            const string DevEUI2 = "ABCDEFABCDEFABC";
            const string GatewayId = "GWDeviceGetterTest1_1";

            this.InitRegistryManager(DevEUI, DevEUI2);

            var items = await DeviceGetter.GetDeviceList(DevEUI, GatewayId, "ABCD", null, this.dummyContext);

            Assert.Single(items);
            Assert.Equal(DevEUI, items[0].DevEUI);
        }

        [Fact]
        public async void DeviceGetter_ABP_Join()
        {
            const string DevEUI = "DEVDeviceGetterTest1_2";
            const string DevEUI2 = "DEVDeviceGetterTest2_2";
            const string GatewayId = "GWDeviceGetterTest1_2";

            this.InitRegistryManager(DevEUI, DevEUI2);

            var items = await DeviceGetter.GetDeviceList(null, GatewayId, "ABCD", "DevAddr1", this.dummyContext);

            Assert.Equal(2, items.Count);
            Assert.Equal(DevEUI, items[0].DevEUI);
            Assert.Equal(DevEUI2, items[1].DevEUI);
        }

        private void InitRegistryManager(string devEui1, string devEui2)
        {
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            this.mockRegistryManager
                .Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                .ReturnsAsync((string deviceId) => new Device(deviceId) { Authentication = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = primaryKey } } });

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

            this.mockRegistryManager
                .Setup(x => x.CreateQuery(It.IsAny<string>(), 100))
                .Returns(queryMock.Object);

            LoRaRegistryManager.InitRegistryManager(this.mockRegistryManager.Object);
        }
    }
}
