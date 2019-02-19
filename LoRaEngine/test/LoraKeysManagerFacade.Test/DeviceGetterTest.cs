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

    public class DeviceGetterTest
    {
        private const string DevEUI = "DEV1";
        private const string DevEUI2 = "DEV2";
        private const string GatewayId = "GW1";
        private const string PrimaryKey = "ABCDEFGH1234567890";

        private readonly Mock<RegistryManager> mockRegistryManager = new Mock<RegistryManager>(MockBehavior.Loose);

        private readonly ExecutionContext dummyContext = new ExecutionContext();

        [Fact]
        public async void DeviceGetter_OTAA_Join()
        {
            this.InitRegistryManager();
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

            var items = await DeviceGetter.GetDeviceList(DevEUI, GatewayId, "ABCD", null, this.dummyContext);

            Assert.Single(items);
            Assert.Equal(DevEUI, items[0].DevEUI);
        }

        [Fact]
        public async void DeviceGetter_ABP_Join()
        {
            this.InitRegistryManager();
            LoRaDeviceCache.InitCacheStore(new LoRaInMemoryDeviceStore());

            var items = await DeviceGetter.GetDeviceList(null, GatewayId, "ABCD", "DevAddr1", this.dummyContext);

            Assert.Equal(2, items.Count);
            Assert.Equal(DevEUI, items[0].DevEUI);
            Assert.Equal(DevEUI2, items[1].DevEUI);
        }

        private void InitRegistryManager()
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

            string[] deviceIds = new string[numberOfDevices] { DevEUI, DevEUI2 };

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
