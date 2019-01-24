// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public class LoRaDeviceRegistryTest
    {
        const string ServerGatewayID = "test-gateway";

        readonly Mock<ILoRaDeviceFactory> loraDeviceFactoryMock;
        readonly Mock<ILoRaDeviceClient> loRaDeviceClient;
        readonly NetworkServerConfiguration serverConfiguration;
        readonly MemoryCache cache;

        public LoRaDeviceRegistryTest()
        {
            this.serverConfiguration = new NetworkServerConfiguration()
            {
                GatewayID = ServerGatewayID,
            };
            this.cache = new MemoryCache(new MemoryCacheOptions());
            this.loraDeviceFactoryMock = new Mock<ILoRaDeviceFactory>(MockBehavior.Strict);
            this.loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
        }

        [Fact]
        public async Task GetDeviceForPayloadAsync_When_Device_Api_Throws_Error_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .Throws(new Exception());
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();
        }

        [Fact]
        public async Task GetDeviceForJoinRequestAsync_When_Device_Api_Throws_Error_Should_Return_Null()
        {
            const string devEUI = "0000000000000001";
            const string appEUI = "0000000000000001";
            const string devNonce = "0001";

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            apiService.Setup(x => x.SearchAndLockForJoinAsync(this.serverConfiguration.GatewayID, devEUI, appEUI, devNonce))
                .Throws(new Exception());
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForJoinRequestAsync(devEUI, appEUI, devNonce);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Not_In_Api_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult());
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Should_Cache_And_Return_It(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            this.loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateABPTwin());

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(actual, createdLoraDevice);
            Assert.True(actual.IsOurDevice);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // ensure device is in cache
            var cachedItem = target.InternalGetCachedDevicesForDevAddr(createdLoraDevice.DevAddr);
            Assert.NotNull(cachedItem);
            Assert.Single(cachedItem);
            Assert.True(cachedItem.TryGetValue(createdLoraDevice.DevEUI, out var actualCachedLoRaDevice));
            Assert.Same(createdLoraDevice, actualCachedLoRaDevice);
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_ABP_Device_Is_Created_Should_Call_Initializers(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            this.loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateABPTwin());

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var initializer = new Mock<ILoRaDeviceInitializer>();
            initializer.Setup(x => x.Initialize(createdLoraDevice));

            target.RegisterDeviceInitializer(initializer.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(actual, createdLoraDevice);
            Assert.True(actual.IsOurDevice);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // initializer was called
            initializer.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Does_Not_Match_Mic_Should_Return_Null(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var cachedSimulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            cachedSimulatedDevice.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"; // make different than the payload
            cachedSimulatedDevice.LoRaDevice.DeviceID = "0000000000000002";

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(cachedSimulatedDevice, this.loRaDeviceClient.Object);
            this.loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // Will get device twin
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync()).ReturnsAsync(new Twin());

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Does_Not_Match_Gateway_Should_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "a_different_one"));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            this.loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(new Twin());

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();
        }

        [Fact]
        public async Task When_Devices_From_Another_Gateway_Is_Cached_Return_Null()
        {
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "another-gateway"));
            var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, null);
            loraDevice1.IsOurDevice = false;

            var existingCache = new DevEUIToLoRaDeviceDictionary();
            this.cache.Set<DevEUIToLoRaDeviceDictionary>(simulatedDevice1.LoRaDevice.DevAddr, existingCache);
            existingCache.TryAdd(loraDevice1.DevEUI, loraDevice1);

            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Multiple_Devices_With_Same_DevAddr_Are_Cached_Should_Find_Matching_By_Mic(string deviceGatewayID)
        {
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, null);

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: this.serverConfiguration.GatewayID));
            simulatedDevice2.LoRaDevice.DeviceID = "00000002";
            simulatedDevice2.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            var loraDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice2, null);

            var existingCache = new DevEUIToLoRaDeviceDictionary();
            this.cache.Set<DevEUIToLoRaDeviceDictionary>(simulatedDevice1.LoRaDevice.DevAddr, existingCache);
            existingCache.TryAdd(loraDevice1.DevEUI, loraDevice1);
            existingCache.TryAdd(loraDevice2.DevEUI, loraDevice2);

            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(loraDevice1, actual);
            Assert.True(actual.IsOurDevice);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Multiple_Devices_With_Same_DevAddr_Are_Returned_From_API_Should_Return_Matching_By_Mic(string deviceGatewayID)
        {
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));

            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey);

            var loRaDeviceClient1 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient1.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice1.CreateABPTwin());

            var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, loRaDeviceClient1.Object);
            var devAddr = loraDevice1.DevAddr;

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice2.LoRaDevice.DeviceID = "00000002";
            simulatedDevice2.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            var loRaDeviceClient2 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient2.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice2.CreateABPTwin());
            var loraDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice2, loRaDeviceClient2.Object);

            // Api service: search devices async
            var iotHubDeviceInfo1 = new IoTHubDeviceInfo(devAddr, loraDevice1.DevEUI, string.Empty);
            var iotHubDeviceInfo2 = new IoTHubDeviceInfo(devAddr, loraDevice2.DevEUI, string.Empty);
            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            apiService.Setup(x => x.SearchByDevAddrAsync(devAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo[]
                {
                    iotHubDeviceInfo2,
                    iotHubDeviceInfo1,
                }));

            // Device factory: create 2 devices
            this.loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo1)).Returns(loraDevice1);
            this.loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo2)).Returns(loraDevice2);

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(loraDevice1, actual);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // Both devices are in cache
            var devicesByDevAddrDictionary = target.InternalGetCachedDevicesForDevAddr(devAddr);
            Assert.NotNull(devicesByDevAddrDictionary);
            Assert.Equal(2, devicesByDevAddrDictionary.Count); // 2 devices with same devAddr exist in cache

            // find device 1
            Assert.True(devicesByDevAddrDictionary.TryGetValue(loraDevice1.DevEUI, out var actualCachedLoRaDevice1));
            Assert.Same(loraDevice1, actualCachedLoRaDevice1);
            Assert.True(loraDevice1.IsOurDevice);

            // find device 2
            Assert.True(devicesByDevAddrDictionary.TryGetValue(loraDevice2.DevEUI, out var actualCachedLoRaDevice2));
            Assert.Same(loraDevice2, actualCachedLoRaDevice2);
            Assert.True(loraDevice2.IsOurDevice);

            loRaDeviceClient1.VerifyAll();
            loRaDeviceClient2.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_New_ABP_Device_Instance_Is_Created_Should_Increment_FCntDown(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            this.loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateABPTwin());

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.NotNull(actual);
            Assert.Same(actual, createdLoraDevice);
            Assert.True(actual.IsOurDevice);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Assigned_To_Another_Gateway_Cache_Locally_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "another-gateway"));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
            this.loraDeviceFactoryMock.Setup(x => x.Create(iotHubDeviceInfo))
                .Returns(createdLoraDevice);

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateABPTwin());

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);

            var actual = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual);

            // search again
            var actual2 = await target.GetDeviceForPayloadAsync(payload);
            Assert.Null(actual2);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // device is in cache
            Assert.Equal(1, this.cache.Count);
            var devAddrDictionary = target.InternalGetCachedDevicesForDevAddr(LoRaTools.Utils.ConversionHelper.ByteArrayToString(payload.DevAddr));
            Assert.NotNull(devAddrDictionary);
            Assert.True(devAddrDictionary.TryGetValue(createdLoraDevice.DevEUI, out var cachedLoRaDevice));
            Assert.False(cachedLoRaDevice.IsOurDevice);
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Cache_Clear_Is_Called_Should_Removed_Cached_Devices(string deviceGatewayID)
        {
            const int deviceCount = 10;
            var foundDeviceList = new HashSet<LoRaDevice>();

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var deviceFactory = new TestLoRaDeviceFactory(this.loRaDeviceClient.Object);
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, deviceFactory);

            var getTwinMockSequence = this.loRaDeviceClient.SetupSequence(x => x.GetTwinAsync());

            for (var deviceID = 1; deviceID <= deviceCount; ++deviceID)
            {
                var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice((uint)deviceID, gatewayID: deviceGatewayID));
                var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
                payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey); // force mic creation

                var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
                apiService.Setup(x => x.SearchByDevAddrAsync(simulatedDevice.DevAddr))
                    .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

                // device will be initialized
                getTwinMockSequence.ReturnsAsync(simulatedDevice.CreateABPTwin());

                var actual = await target.GetDeviceForPayloadAsync(payload);
                Assert.NotNull(actual);
                foundDeviceList.Add(actual);
            }

            Assert.Equal(deviceCount, foundDeviceList.Count);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // ensure all devices are in cache
            Assert.Equal(deviceCount, foundDeviceList.Count(x => target.InternalGetCachedDevicesForDevAddr(x.DevAddr).Count == 1));

            target.ResetDeviceCache();
            Assert.False(foundDeviceList.Any(x => target.InternalGetCachedDevicesForDevAddr(x.DevAddr).Count > 0), "Should not find devices again");
        }
    }
}
