// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.FacadeTests
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTHubImp;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices;
    using Microsoft.Azure.Devices.Shared;
    using Moq;
    using Xunit;

    public class IoTHubRegistryManagerTest : FunctionTestBase
    {
        private readonly string iotHubHostName = "fake.azure-devices.net";

        [Fact]
        // This test ensure that IoT Hub implementation of DeviceRegistry Reflects the expected pagination features
        public async Task Page_Result_Executed()
        {
            var deviceId = "device-123";

            var twins = Enumerable.Range(0, 7)
                                .Select(c => new Twin(NewUniqueEUI64()));

            var pageIndex = 0;
            var pageSize = 5;

            var queryMock = new Mock<IQuery>(MockBehavior.Strict);
            queryMock.SetupGet(c => c.HasMoreResults)
                .Returns(() => (pageIndex * pageSize) < twins.Count());

            queryMock.Setup(c => c.GetNextAsTwinAsync())
                .ReturnsAsync(() => twins.Skip(pageIndex++ * pageSize).Take(pageSize));

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.CreateQuery(It.IsAny<string>()))
                   .Returns((string x) => queryMock.Object);

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var result = await deviceRegistry.FindDeviceByAddrAsync(deviceId);

            iotHubMock.Verify(x => x.CreateQuery(It.IsAny<string>()));

            Assert.NotNull(result);
            Assert.True(result.HasMoreResults);

            // Asserts that first page is present
            var page = await result.GetNextPageAsync();
            Assert.Equal(pageSize, page.Count());
            queryMock.Verify(c => c.GetNextAsTwinAsync(), Times.Once());
            Assert.True(result.HasMoreResults);

            // Assets that only second page is present
            page = await result.GetNextPageAsync();
            Assert.False(result.HasMoreResults);
            Assert.Equal(2, page.Count());
            queryMock.Verify(c => c.GetNextAsTwinAsync(), Times.Exactly(2));
        }

        [Fact]
        // This test ensure that ioT Hub implementation of DeviceRegistry calls the IoT Hub with correct parameters and returns the correspondig answer
        public async Task Get_Device_Call_IoTHub()
        {
            var deviceId = "xs7fwdsobs";

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                   .ReturnsAsync((string x) => new Device(x));

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var device = await deviceRegistry.GetDeviceAsync(deviceId);

            Assert.NotNull(device);
            Assert.Equal(deviceId, device.DeviceId);

            iotHubMock.Verify(x => x.GetDeviceAsync(It.Is<string>(c => c == deviceId)), Times.Once());
        }

        [Fact]
        // This test ensure that ioT Hub implementation of DeviceRegistry calls the IoT Hub with correct parameters and a null device (when IoT hub returns a null object)
        public async Task Get_Device_NotExisting_Should_Return_Null()
        {
            var deviceId = "device-123";

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                   .ReturnsAsync((string x) => null);

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var device = await deviceRegistry.GetDeviceAsync(deviceId);

            Assert.Null(device);

            iotHubMock.Verify(x => x.GetDeviceAsync(It.Is<string>(c => c == deviceId)), Times.Once());
        }

        [Fact]
        // This test ensure that ioT Hub implementation of DeviceRegistry calls the IoT Hub with correct parameters and returns the correspondig answer
        public async Task Get_Twin_Call_IoTHub()
        {
            var deviceId = "device-123";

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.GetTwinAsync(It.IsAny<string>()))
                   .ReturnsAsync((string x) => new Twin(x));

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var device = await deviceRegistry.GetTwinAsync(deviceId);

            Assert.NotNull(device);
            Assert.Equal(deviceId, device.DeviceId);

            iotHubMock.Verify(x => x.GetTwinAsync(It.Is<string>(c => c == deviceId)), Times.Once());
        }

        [Fact]
        // This test ensure that ioT Hub implementation of DeviceRegistry calls the IoT Hub with correct parameters and a null device (when IoT hub returns a null object)
        public async Task Get_Twin_NotExisting_Should_Return_Null()
        {
            var deviceId = "device-123";

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.GetTwinAsync(It.IsAny<string>()))
                   .ReturnsAsync((string x) => null);

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var device = await deviceRegistry.GetTwinAsync(deviceId);

            Assert.Null(device);

            iotHubMock.Verify(x => x.GetTwinAsync(It.Is<string>(c => c == deviceId)), Times.Once());
        }

        [Fact]
        // This test ensure that ioT Hub implementation of DeviceRegistry calls the IoT Hub with correct parameters and returns the correspondig answer
        public async Task Find_Device_By_Addr_Query_IoTHub()
        {
            var twins = Enumerable.Range(0, 10)
                                .Select(c => new Twin(NewUniqueEUI64()));

            var queryMock = new Mock<IQuery>(MockBehavior.Strict);
            queryMock.SetupGet(c => c.HasMoreResults)
                .Returns(true);
            queryMock.Setup(c => c.GetNextAsTwinAsync())
                .ReturnsAsync(() => twins);

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.CreateQuery(It.IsAny<string>()))
                   .Returns((string x) => queryMock.Object);

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var result = await deviceRegistry.FindDeviceByAddrAsync(NewUniqueEUI64());

            Assert.NotNull(result);
            Assert.True(result.HasMoreResults);

            var page = await result.GetNextPageAsync();
            Assert.Equal(twins.Count(), page.Count());

            queryMock.Verify(c => c.GetNextAsTwinAsync(), Times.Once());
            iotHubMock.Verify(x => x.CreateQuery(It.Is<string>(c => c.StartsWith("SELECT * FROM devices WHERE properties.desired.DevAddr", StringComparison.OrdinalIgnoreCase))), Times.Once());
        }

        [Fact]
        public async Task Find_Devices_By_LastUpdate_Date_Query_IoTHub()
        {
            var twins = Enumerable.Range(0, 10)
                                .Select(c => new Twin(NewUniqueEUI64()));

            var queryMock = new Mock<IQuery>(MockBehavior.Strict);
            queryMock.SetupGet(c => c.HasMoreResults)
                .Returns(true);
            queryMock.Setup(c => c.GetNextAsTwinAsync())
                .ReturnsAsync(() => twins);

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.CreateQuery(It.IsAny<string>()))
                   .Returns((string x) => queryMock.Object);

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var result = await deviceRegistry.FindDevicesByLastUpdateDate(DateTime.UtcNow.AddMinutes(-5).ToString(CultureInfo.GetCultureInfo("en-US")));

            Assert.NotNull(result);
            Assert.True(result.HasMoreResults);

            var page = await result.GetNextPageAsync();
            Assert.Equal(twins.Count(), page.Count());

            queryMock.Verify(c => c.GetNextAsTwinAsync(), Times.Once());
            iotHubMock.Verify(x => x.CreateQuery(It.Is<string>(c => c.StartsWith("SELECT * FROM devices where properties.desired.$metadata.$lastUpdated >= ", StringComparison.OrdinalIgnoreCase))), Times.Once());
        }

        [Fact]
        public async Task Find_Configured_LoRaDevices_Query_IoTHub()
        {
            var twins = Enumerable.Range(0, 10)
                                .Select(c => new Twin(NewUniqueEUI64()));

            var queryMock = new Mock<IQuery>(MockBehavior.Strict);
            queryMock.SetupGet(c => c.HasMoreResults)
                .Returns(true);
            queryMock.Setup(c => c.GetNextAsTwinAsync())
                .ReturnsAsync(() => twins);

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.CreateQuery(It.IsAny<string>()))
                   .Returns((string x) => queryMock.Object);

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var result = await deviceRegistry.FindConfiguredLoRaDevices();

            Assert.NotNull(result);
            Assert.True(result.HasMoreResults);

            var page = await result.GetNextPageAsync();
            Assert.Equal(twins.Count(), page.Count());

            queryMock.Verify(c => c.GetNextAsTwinAsync(), Times.Once());
            iotHubMock.Verify(x => x.CreateQuery(It.Is<string>(c => c.StartsWith("SELECT * FROM devices WHERE is_defined(properties.desired.AppKey)", StringComparison.OrdinalIgnoreCase))), Times.Once());
        }

        [Fact]
        public async Task Get_Device_PrimaryKey()
        {
            var deviceId = "device-123";
            var expectedPrimaryKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("ABCDEFGH1234567890"));

            var iotHubMock = new Mock<RegistryManager>(MockBehavior.Strict);

            iotHubMock.Setup(x => x.GetDeviceAsync(It.IsAny<string>()))
                   .ReturnsAsync((string x) => new Device(x)
                   {
                       Authentication = new AuthenticationMechanism() { SymmetricKey = new SymmetricKey() { PrimaryKey = expectedPrimaryKey } }
                   });

            var deviceRegistry = new IoTHubDeviceRegistryManager(iotHubMock.Object, this.iotHubHostName);

            var primaryKey = await deviceRegistry.GetDevicePrimaryKey(deviceId);

            Assert.NotNull(primaryKey);
            Assert.Equal(expectedPrimaryKey, primaryKey);

            iotHubMock.Verify(x => x.GetDeviceAsync(It.Is<string>(c => c == deviceId)), Times.Once());
        }
    }
}
