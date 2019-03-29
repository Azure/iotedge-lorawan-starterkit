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

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Should_Cache_And_Process_Request(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            // device will be initialized
            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateABPTwin());

            var request = new WaitableLoRaRequest(payload);
            var requestHandler = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            requestHandler.Setup(x => x.ProcessRequestAsync(request, It.IsNotNull<LoRaDevice>()))
                .ReturnsAsync(new LoRaDeviceRequestProcessResult(null, request));

            var deviceFactory = new TestLoRaDeviceFactory(this.loRaDeviceClient.Object, requestHandler.Object);

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, deviceFactory);
            target.GetLoRaRequestQueue(request).Queue(request);

            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // ensure device is in cache
            var cachedItem = target.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
            Assert.NotNull(cachedItem);
            Assert.Single(cachedItem);
            Assert.True(cachedItem.TryGetValue(simulatedDevice.DevEUI, out var actualCachedLoRaDevice));

            // request was handled
            requestHandler.VerifyAll();
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

            var request = new WaitableLoRaRequest(payload);
            target.GetLoRaRequestQueue(request).Queue(request);
            Assert.True(await request.WaitCompleteAsync());

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // initializer was called
            initializer.VerifyAll();
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
            var request = new WaitableLoRaRequest(payload);
            var queue = target.GetLoRaRequestQueue(request);
            queue.Queue(request);
            Assert.IsType<ExternalGatewayLoRaRequestQueue>(queue);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request.ProcessingFailedReason);

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

            var request = new WaitableLoRaRequest(payload);
            var requestHandler = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            requestHandler.Setup(x => x.ProcessRequestAsync(request, loraDevice1))
                .ReturnsAsync(new LoRaDeviceRequestProcessResult(loraDevice1, request));
            loraDevice1.SetRequestHandler(requestHandler.Object);

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object);
            target.GetLoRaRequestQueue(request).Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            requestHandler.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_Queueing_To_Multiple_Devices_With_Same_DevAddr_Should_Queue_To_Device_Matching_Mic(string deviceGatewayID)
        {
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));

            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey);

            var loRaDeviceClient1 = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient1.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice1.CreateABPTwin());

            var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, loRaDeviceClient1.Object);
            var devAddr = loraDevice1.DevAddr;

            WaitableLoRaRequest request = null;
            var reqHandler1 = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            reqHandler1.Setup(x => x.ProcessRequestAsync(It.IsNotNull<LoRaRequest>(), loraDevice1))
                .ReturnsAsync(new LoRaDeviceRequestProcessResult(loraDevice1, request));
            loraDevice1.SetRequestHandler(reqHandler1.Object);

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
            request = new WaitableLoRaRequest(payload);
            target.GetLoRaRequestQueue(request).Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

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

            reqHandler1.VerifyAll();
            loRaDeviceClient1.VerifyAll();
            loRaDeviceClient2.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Assigned_To_Another_Gateway_Cache_Locally_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "another-gateway"));

            var apiService = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            this.loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simulatedDevice.CreateABPTwin());
            this.loRaDeviceClient.Setup(x => x.Disconnect())
                .Returns(true);

            var deviceFactory = new TestLoRaDeviceFactory(this.loRaDeviceClient.Object);

            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, deviceFactory);

            // request #1
            var payload1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 11);
            payload1.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);
            var request1 = new WaitableLoRaRequest(payload1);
            target.GetLoRaRequestQueue(request1).Queue(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request1.ProcessingFailedReason);

            // request #2
            var payload2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 12);
            payload2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);
            var request2 = new WaitableLoRaRequest(payload2);
            target.GetLoRaRequestQueue(request2).Queue(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.True(request2.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request2.ProcessingFailedReason);

            // Device was searched by DevAddr
            apiService.VerifyAll();
            apiService.Verify(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()), Times.Once());

            this.loRaDeviceClient.VerifyAll();
            this.loRaDeviceClient.Verify(x => x.GetTwinAsync(), Times.Once());

            // device is in cache
            var devAddrDictionary = target.InternalGetCachedDevicesForDevAddr(LoRaTools.Utils.ConversionHelper.ByteArrayToString(payload1.DevAddr));
            Assert.NotNull(devAddrDictionary);
            Assert.True(devAddrDictionary.TryGetValue(simulatedDevice.DevEUI, out var cachedLoRaDevice));
            Assert.False(cachedLoRaDevice.IsOurDevice);
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public void When_Cache_Clear_Is_Called_Should_Removed_Cached_Devices(string deviceGatewayID)
        {
            const int deviceCount = 10;
            var deviceList = new HashSet<LoRaDevice>();

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var deviceFactory = new TestLoRaDeviceFactory(this.loRaDeviceClient.Object);
            var target = new LoRaDeviceRegistry(this.serverConfiguration, this.cache, apiService.Object, deviceFactory);

            var getTwinMockSequence = this.loRaDeviceClient.SetupSequence(x => x.GetTwinAsync());

            for (var deviceID = 1; deviceID <= deviceCount; ++deviceID)
            {
                var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice((uint)deviceID, gatewayID: deviceGatewayID));
                var dict = target.InternalGetCachedDevicesForDevAddr(simulatedDevice.DevAddr);
                var device = TestUtils.CreateFromSimulatedDevice(simulatedDevice, this.loRaDeviceClient.Object);
                deviceList.Add(device);
                dict.TryAdd(simulatedDevice.DevEUI, device);
            }

            Assert.Equal(deviceCount, this.cache.Count);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // ensure all devices are in cache
            Assert.Equal(deviceCount, deviceList.Count(x => target.InternalGetCachedDevicesForDevAddr(x.DevAddr).Count == 1));

            target.ResetDeviceCache();
            Assert.False(deviceList.Any(x => target.InternalGetCachedDevicesForDevAddr(x.DevAddr).Count > 0), "Should not find devices again");
        }

        [Fact]
        public async Task When_Loading_Device_By_DevAddr_Should_Be_Able_To_Load_By_DevEUI()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            deviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simDevice.CreateABPTwin());

            var handlerImplementation = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            var deviceFactory = new TestLoRaDeviceFactory(deviceClient.Object, handlerImplementation.Object);

            var deviceRegistry = new LoRaDeviceRegistry(
                this.serverConfiguration,
                this.cache,
                deviceApi.Object,
                deviceFactory);

            var payload = simDevice.CreateUnconfirmedDataUpMessage("1");
            payload.SerializeUplink(simDevice.AppSKey, simDevice.NwkSKey);
            var request = new WaitableLoRaRequest(payload);

            deviceRegistry.GetLoRaRequestQueue(request).Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            await Task.Delay(50);
            Assert.NotNull(await deviceRegistry.GetDeviceByDevEUIAsync(simDevice.DevEUI));

            handlerImplementation.VerifyAll();
            deviceApi.VerifyAll();
            deviceClient.VerifyAll();
            deviceClient.Verify(x => x.GetTwinAsync(), Times.Once());
        }

        [Fact]
        public async Task When_Loading_Device_By_DevEUI_Should_Be_Able_To_Load_By_DevAddr()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            deviceApi.Setup(x => x.SearchByDevEUIAsync(simDevice.DevEUI))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            deviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(simDevice.CreateABPTwin());

            var handlerImplementation = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            handlerImplementation.Setup(x => x.ProcessRequestAsync(It.IsNotNull<LoRaRequest>(), It.IsNotNull<LoRaDevice>()))
                .Returns<LoRaRequest, LoRaDevice>((req, device) =>
                {
                    return Task.FromResult(new LoRaDeviceRequestProcessResult(device, req));
                });

            var deviceFactory = new TestLoRaDeviceFactory(deviceClient.Object, handlerImplementation.Object);

            var deviceRegistry = new LoRaDeviceRegistry(
                this.serverConfiguration,
                this.cache,
                deviceApi.Object,
                deviceFactory);

            Assert.NotNull(await deviceRegistry.GetDeviceByDevEUIAsync(simDevice.DevEUI));

            var payload = simDevice.CreateUnconfirmedDataUpMessage("1");
            payload.SerializeUplink(simDevice.AppSKey, simDevice.NwkSKey);
            var request = new WaitableLoRaRequest(payload);

            deviceRegistry.GetLoRaRequestQueue(request).Queue(request);
            Assert.True(await request.WaitCompleteAsync());

            handlerImplementation.VerifyAll();
            deviceApi.VerifyAll();
            deviceClient.VerifyAll();

            deviceClient.Verify(x => x.GetTwinAsync(), Times.Once());
        }

        [Fact]
        public async Task GetDeviceByDevEUIAsync_When_Api_Returns_Null_Should_Return_Null()
        {
            var deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            deviceApi.Setup(x => x.SearchByDevEUIAsync(It.IsNotNull<string>()))
                .ReturnsAsync((SearchDevicesResult)null);

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            var deviceFactory = new TestLoRaDeviceFactory(deviceClient.Object);

            var deviceRegistry = new LoRaDeviceRegistry(
                this.serverConfiguration,
                this.cache,
                deviceApi.Object,
                deviceFactory);

            var actual = await deviceRegistry.GetDeviceByDevEUIAsync("1");
            Assert.Null(actual);

            deviceApi.VerifyAll();
            deviceClient.VerifyAll();
        }

        [Fact]
        public async Task GetDeviceByDevEUIAsync_When_Api_Returns_Empty_Should_Return_Null()
        {
            var deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            deviceApi.Setup(x => x.SearchByDevEUIAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult());

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            var deviceFactory = new TestLoRaDeviceFactory(deviceClient.Object);

            var deviceRegistry = new LoRaDeviceRegistry(
                this.serverConfiguration,
                this.cache,
                deviceApi.Object,
                deviceFactory);

            var actual = await deviceRegistry.GetDeviceByDevEUIAsync("1");
            Assert.Null(actual);

            deviceApi.VerifyAll();
            deviceClient.VerifyAll();
        }
    }
}
