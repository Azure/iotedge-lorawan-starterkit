// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System;
    using System.Text;
    using System.Threading;
    using global::LoraKeysManagerFacade;
    using global::LoRaTools;
    using global::LoRaTools.IoTHubImpl;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class DeviceGetterTest : FunctionTestBase
    {
        private const string PrimaryKey = "ABCDEFGH1234567890";

        [Fact]
        public async void DeviceGetter_OTAA_Join()
        {
            var devEui = TestEui.GenerateDevEui();
            var gatewayId = NewUniqueEUI64();

            var deviceGetter = new DeviceGetter(InitRegistryManager(devEui), new LoRaInMemoryDeviceStore(), NullLogger<DeviceGetter>.Instance);
            var items = await deviceGetter.GetDeviceList(devEui, gatewayId, new DevNonce(0xABCD), null);

            Assert.Single(items);
            Assert.Equal(devEui, items[0].DevEUI);
        }

        private static IDeviceRegistryManager InitRegistryManager(DevEui devEui)
        {
            var mockRegistryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));
            mockRegistryManager
                .Setup(x => x.GetDevicePrimaryKeyAsync(It.Is(devEui.ToString(), StringComparer.Ordinal)))
                .ReturnsAsync((string _) => primaryKey);

            mockRegistryManager
                .Setup(x => x.GetLoRaDeviceTwinAsync(It.Is(devEui.ToString(), StringComparer.Ordinal), It.IsAny<CancellationToken?>()))
                .ReturnsAsync((string deviceId, CancellationToken _) => new IoTHubLoRaDeviceTwin (new Twin(deviceId)));

            return mockRegistryManager.Object;
        }
    }
}
