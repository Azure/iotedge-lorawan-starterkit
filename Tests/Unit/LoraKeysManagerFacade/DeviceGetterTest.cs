// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System;
    using System.Text;
    using global::LoraKeysManagerFacade;
    using LoRaWan.Tests.Common;
    using Moq;
    using Xunit;

    public class DeviceGetterTest : FunctionTestBase
    {
        private const string PrimaryKey = "ABCDEFGH1234567890";
        protected const string IotHubHostName = "fake.azure-devices.net";

        private static DevAddr CreateDevAddr() => new DevAddr((uint)RandomNumberGenerator.GetInt32(int.MaxValue));

        [Fact]
        public async void DeviceGetter_OTAA_Join()
        {
            var devEui = TestEui.GenerateDevEui();
            var gatewayId = NewUniqueEUI64();

            var deviceGetter = new DeviceGetter(InitRegistryManager(devEui), new LoRaInMemoryDeviceStore());
            var items = await deviceGetter.GetDeviceList(devEui, gatewayId, new DevNonce(0xABCD), null);

            Assert.Single(items);
            Assert.Equal(devEui, items[0].DevEUI);
        }

        private static RegistryManager InitRegistryManager(DevEui devEui)
        {
            var mockRegistryManager = new Mock<IDeviceRegistryManager>(MockBehavior.Strict);
            var primaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(PrimaryKey));

            mockRegistryManager
                .Setup(x => x.GetDeviceAsync(It.Is(devEui.ToString(), StringComparer.Ordinal)))
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
                .Setup(x => x.GetTwinAsync(It.Is(devEui.ToString(), StringComparer.Ordinal)))
                .ReturnsAsync((string deviceId) =>
                {
                    var mockDevice = new Mock<IDeviceTwin>(MockBehavior.Strict);

                    mockDevice.SetupGet(t => t.DeviceId)
                        .Returns(deviceId);
                    mockDevice.SetupGet(t => t.DevAddr)
                              .Returns(CreateDevAddr());
                    mockDevice.SetupGet(t => t.GatewayID)
                              .Returns(string.Empty);
                    mockDevice.SetupGet(t => t.LastUpdated)
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
                    mockDevice.SetupGet(t => t.DevAddr)
                              .Returns(CreateDevAddr());
                    mockDevice.SetupGet(t => t.GatewayID)
                              .Returns(string.Empty);
                    mockDevice.SetupGet(t => t.LastUpdated)
                              .Returns(DateTime.UtcNow);

                    yield return mockDevice.Object;
                }
            }

            queryMock
                .Setup(x => x.GetNextPageAsync())
                .ReturnsAsync(Twins());

            mockRegistryManager
                .Setup(x => x.FindDeviceByAddrAsync(It.IsAny<DevAddr>()))
                .ReturnsAsync(queryMock.Object);

            return mockRegistryManager.Object;
        }
    }
}
