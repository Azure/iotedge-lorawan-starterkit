// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public sealed class LoRaDeviceRegistryTest : MessageProcessorTestBase
    {
        private readonly MemoryCache cache;
        private readonly Mock<ILoRaDeviceFactory> loraDeviceFactoryMock;

        public LoRaDeviceRegistryTest() : base()
        {
            this.loraDeviceFactoryMock = new Mock<ILoRaDeviceFactory>(MockBehavior.Strict);
            this.cache = new MemoryCache(new MemoryCacheOptions());
        }

        [Fact]
        public async Task GetDeviceForJoinRequestAsync_When_Device_Api_Throws_Error_Should_Not_Catch()
        {
            const string devEUI = "0000000000000001";
            const string devNonce = "0001";

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            apiService.Setup(x => x.SearchAndLockForJoinAsync(ServerConfiguration.GatewayID, devEUI, devNonce))
                .Throws(new InvalidOperationException());
            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object, DeviceCache);

            Task Act() => target.GetDeviceForJoinRequestAsync(devEUI, devNonce);
            _ = await Assert.ThrowsAsync<InvalidOperationException>(Act);

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
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "pk") { GatewayId = deviceGatewayID };
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            // device will be initialized
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simulatedDevice.CreateABPTwin());

            using var request = WaitableLoRaRequest.Create(payload);
            var requestHandler = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            requestHandler.Setup(x => x.ProcessRequestAsync(request, It.IsNotNull<LoRaDevice>()))
                .ReturnsAsync(new LoRaDeviceRequestProcessResult(null, request));

            var deviceFactory = new TestLoRaDeviceFactory(LoRaDeviceClient.Object, requestHandler.Object, DeviceCache, ConnectionManager);

            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, deviceFactory, DeviceCache);
            target.GetLoRaRequestQueue(request).Queue(request);

            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // ensure device is in cache
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var actualCachedLoRaDevice));

            // request was handled
            requestHandler.VerifyAll();
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public async Task When_ABP_Device_Is_Created_Should_Call_Initializers(string deviceGatewayID)
        {
            LoRaDeviceClient.Setup(ldc => ldc.Dispose());
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            using var connectionManager = new SingleDeviceConnectionManager(LoRaDeviceClient.Object);
            using var createdLoraDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, connectionManager);
            this.loraDeviceFactoryMock.Setup(x => x.CreateAndRegisterAsync(iotHubDeviceInfo, It.IsAny<CancellationToken>()))
                .ReturnsAsync(createdLoraDevice);

            // device will be initialized
            LoRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simulatedDevice.CreateABPTwin());

            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object, DeviceCache);

            var initializer = new Mock<ILoRaDeviceInitializer>();
            initializer.Setup(x => x.Initialize(createdLoraDevice));

            target.RegisterDeviceInitializer(initializer.Object);

            using var request = WaitableLoRaRequest.Create(payload);
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
            using var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, ConnectionManager);
            loraDevice1.IsOurDevice = false;

            DeviceCache.Register(loraDevice1);

            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);

            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object, DeviceCache);
            using var request = WaitableLoRaRequest.Create(payload);
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
            var connectionManager = new Mock<ILoRaDeviceClientConnectionManager>();
            var simulatedDevice1 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));

            using var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, connectionManager.Object);

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: ServerConfiguration.GatewayID));
            simulatedDevice2.LoRaDevice.DeviceID = "00000002";
            simulatedDevice2.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            using var loraDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice2, connectionManager.Object);

            DeviceCache.Register(loraDevice1);
            DeviceCache.Register(loraDevice2);
            
            var payload = simulatedDevice1.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice1.AppSKey, simulatedDevice1.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();

            using var request = WaitableLoRaRequest.Create(payload);
            var requestHandler = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            requestHandler.Setup(x => x.ProcessRequestAsync(request, loraDevice1))
                .ReturnsAsync(new LoRaDeviceRequestProcessResult(loraDevice1, request));
            loraDevice1.SetRequestHandler(requestHandler.Object);

            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object, DeviceCache);
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

            var loRaDeviceClient1 = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            loRaDeviceClient1.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simulatedDevice1.CreateABPTwin());

            using var connectionManager1 = new SingleDeviceConnectionManager(loRaDeviceClient1.Object);
            using var loraDevice1 = TestUtils.CreateFromSimulatedDevice(simulatedDevice1, connectionManager1);
            var devAddr = loraDevice1.DevAddr;

            var reqHandler1 = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            reqHandler1.Setup(x => x.ProcessRequestAsync(It.IsNotNull<LoRaRequest>(), loraDevice1))
                .ReturnsAsync(new LoRaDeviceRequestProcessResult(loraDevice1, null));
            loraDevice1.SetRequestHandler(reqHandler1.Object);

            var simulatedDevice2 = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));
            simulatedDevice2.LoRaDevice.DeviceID = "00000002";
            simulatedDevice2.LoRaDevice.NwkSKey = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            var loRaDeviceClient2 = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            loRaDeviceClient2.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simulatedDevice2.CreateABPTwin());
            using var connectionManager2 = new SingleDeviceConnectionManager(loRaDeviceClient2.Object);
            using var loraDevice2 = TestUtils.CreateFromSimulatedDevice(simulatedDevice2, connectionManager2);

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
            this.loraDeviceFactoryMock.Setup(x => x.CreateAndRegisterAsync(iotHubDeviceInfo1, It.IsAny<CancellationToken>())).ReturnsAsync(() => {
                DeviceCache.Register(loraDevice1);
                return loraDevice1;
            });
            this.loraDeviceFactoryMock.Setup(x => x.CreateAndRegisterAsync(iotHubDeviceInfo2, It.IsAny<CancellationToken>())).ReturnsAsync(() => {
                DeviceCache.Register(loraDevice2);
                return loraDevice2;
            });

            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, this.loraDeviceFactoryMock.Object, DeviceCache);
            using var request = WaitableLoRaRequest.Create(payload);
            target.GetLoRaRequestQueue(request).Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingSucceeded);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // Both devices are in cache
            Assert.Equal(2, DeviceCache.RegistrationCount(devAddr)); // 2 devices with same devAddr exist in cache

            // find device 1
            Assert.True(DeviceCache.TryGetForPayload(request.Payload, out var actualCachedLoRaDevice1));
            Assert.Same(loraDevice1, actualCachedLoRaDevice1);
            Assert.True(loraDevice1.IsOurDevice);

            // find device 2
            Assert.True(DeviceCache.TryGetByDevEui(loraDevice2.DevEUI, out var actualCachedLoRaDevice2));
            Assert.Same(loraDevice2, actualCachedLoRaDevice2);
            Assert.True(loraDevice2.IsOurDevice);

            reqHandler1.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Is_Assigned_To_Another_Gateway_Cache_Locally_And_Return_Null()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "another-gateway"));

            var apiService = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "pk")
            {
                GatewayId = "another-gateway",
                NwkSKey = simulatedDevice.NwkSKey
            };
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var deviceFactory = new TestLoRaDeviceFactory(LoRaDeviceClient.Object, DeviceCache, ConnectionManager);

            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, deviceFactory, DeviceCache);

            // request #1
            var payload1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1", fcnt: 11);
            payload1.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);
            using var request1 = WaitableLoRaRequest.Create(payload1);
            target.GetLoRaRequestQueue(request1).Queue(request1);
            Assert.True(await request1.WaitCompleteAsync());
            Assert.True(request1.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request1.ProcessingFailedReason);

            // request #2
            var payload2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2", fcnt: 12);
            payload2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);
            using var request2 = WaitableLoRaRequest.Create(payload2);
            target.GetLoRaRequestQueue(request2).Queue(request2);
            Assert.True(await request2.WaitCompleteAsync());
            Assert.True(request2.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request2.ProcessingFailedReason);

            // Device was searched by DevAddr
            apiService.VerifyAll();
            apiService.Verify(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()), Times.Once());

            // Device should not be connected
            LoRaDeviceClient.VerifyAll();
            LoRaDeviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Never());
            LoRaDeviceClient.Verify(x => x.Disconnect(), Times.Never());

            // device is in cache
            Assert.True(DeviceCache.TryGetForPayload(request1.Payload, out var loRaDevice));
            Assert.False(loRaDevice.IsOurDevice);
        }

        [Fact]
        public async Task When_Device_Is_Assigned_To_Another_Gateway_After_No_Connection_Should_Be_Established()
        {
            const string gatewayId = "another-gateway";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: gatewayId));

            var apiService = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "pk") { NwkSKey = simulatedDevice.NwkSKey, GatewayId = gatewayId };
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var deviceFactory = new TestLoRaDeviceFactory(LoRaDeviceClient.Object, DeviceCache, ConnectionManager);

            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, deviceFactory, DeviceCache);

            // setup 2 requests - ensure the cache is validated before fetching from the function
            var requests = Enumerable.Range(1, 2).Select((n) =>
                {
                    var payload = simulatedDevice.CreateUnconfirmedDataUpMessage(n.ToString(CultureInfo.InvariantCulture), fcnt: (uint)n + 10);
                    payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);
                    var request = WaitableLoRaRequest.Create(payload);
                    target.GetLoRaRequestQueue(request).Queue(request);
                    return request;
                }
            ).ToList();

            foreach (var request in requests)
            {
                Assert.True(await request.WaitCompleteAsync());
                Assert.True(request.ProcessingFailed);
                Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request.ProcessingFailedReason);
            }

            // Device was searched by DevAddr
            apiService.VerifyAll();
            apiService.Verify(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()), Times.Once());

            LoRaDeviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Never());

            // device is in cache
            Assert.True(DeviceCache.TryGetForPayload(requests.First().Payload, out var cachedLoRaDevice));
            Assert.False(cachedLoRaDevice.IsOurDevice);
        }

        [Theory]
        [InlineData(ServerGatewayID)]
        [InlineData(null)]
        public void When_Cache_Clear_Is_Called_Should_Removed_Cached_Devices(string deviceGatewayID)
        {
            LoRaDeviceClient.Setup(ldc => ldc.Dispose());
            const int deviceCount = 10;
            var deviceList = new HashSet<LoRaDevice>();

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var deviceFactory = new TestLoRaDeviceFactory(LoRaDeviceClient.Object, DeviceCache);
            using var target = new LoRaDeviceRegistry(ServerConfiguration, this.cache, apiService.Object, deviceFactory, DeviceCache);
            using var connectionManager = new SingleDeviceConnectionManager(LoRaDeviceClient.Object);

            for (var deviceID = 1; deviceID <= deviceCount; ++deviceID)
            {
                var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice((uint)deviceID, gatewayID: deviceGatewayID));
#pragma warning disable CA2000 // Dispose objects before losing scope - transfer ownership
                var device = TestUtils.CreateFromSimulatedDevice(simulatedDevice, connectionManager);
#pragma warning restore CA2000 // Dispose objects before losing scope
                DeviceCache.Register(device);
                deviceList.Add(device);
            }

            Assert.Equal(deviceCount, DeviceCache.CalculateStatistics().Count);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            this.loraDeviceFactoryMock.VerifyAll();

            // ensure all devices are in cache
            Assert.Equal(deviceCount, deviceList.Count(x => DeviceCache.TryGetByDevEui(x.DevEUI, out _)));

            target.ResetDeviceCache();
            Assert.False(deviceList.Any(x => DeviceCache.TryGetByDevEui(x.DevEUI, out _)), "Should not find devices again");
        }

        [Fact]
        public async Task When_Loading_Device_By_DevAddr_Should_Be_Able_To_Load_By_DevEUI()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            deviceApi.Setup(x => x.SearchByDevAddrAsync(simDevice.DevAddr))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simDevice.CreateABPTwin());

            var handlerImplementation = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            var deviceFactory = new TestLoRaDeviceFactory(deviceClient.Object, handlerImplementation.Object, DeviceCache, ConnectionManager);

            using var deviceRegistry = new LoRaDeviceRegistry(
                ServerConfiguration,
                this.cache,
                deviceApi.Object,
                deviceFactory,
                DeviceCache);

            var payload = simDevice.CreateUnconfirmedDataUpMessage("1");
            payload.SerializeUplink(simDevice.AppSKey, simDevice.NwkSKey);
            using var request = WaitableLoRaRequest.Create(payload);

            deviceRegistry.GetLoRaRequestQueue(request).Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            await Task.Delay(50);
            Assert.NotNull(await deviceRegistry.GetDeviceByDevEUIAsync(simDevice.DevEUI));

            handlerImplementation.VerifyAll();
            deviceApi.VerifyAll();
            deviceClient.VerifyAll();
            deviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());
        }

        [Fact]
        public async Task When_Loading_Device_By_DevEUI_Should_Be_Able_To_Load_By_DevAddr()
        {
            var simDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));

            var deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            deviceApi.Setup(x => x.SearchByEuiAsync(DevEui.Parse(simDevice.DevEUI)))
                .ReturnsAsync(new SearchDevicesResult(new IoTHubDeviceInfo(simDevice.DevAddr, simDevice.DevEUI, "123").AsList()));

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            deviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simDevice.CreateABPTwin());

            var handlerImplementation = new Mock<ILoRaDataRequestHandler>(MockBehavior.Strict);
            handlerImplementation.Setup(x => x.ProcessRequestAsync(It.IsNotNull<LoRaRequest>(), It.IsNotNull<LoRaDevice>()))
                .Returns<LoRaRequest, LoRaDevice>((req, device) =>
                {
                    return Task.FromResult(new LoRaDeviceRequestProcessResult(device, req));
                });

            var deviceFactory = new TestLoRaDeviceFactory(deviceClient.Object, handlerImplementation.Object, DeviceCache, ConnectionManager);

            using var deviceRegistry = new LoRaDeviceRegistry(
                ServerConfiguration,
                this.cache,
                deviceApi.Object,
                deviceFactory,
                DeviceCache);

            Assert.NotNull(await deviceRegistry.GetDeviceByDevEUIAsync(simDevice.DevEUI));

            var payload = simDevice.CreateUnconfirmedDataUpMessage("1");
            payload.SerializeUplink(simDevice.AppSKey, simDevice.NwkSKey);
            using var request = WaitableLoRaRequest.Create(payload);

            deviceRegistry.GetLoRaRequestQueue(request).Queue(request);
            Assert.True(await request.WaitCompleteAsync());

            handlerImplementation.VerifyAll();
            deviceApi.VerifyAll();
            deviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Once());
        }

        [Fact]
        public async Task GetDeviceByDevEUIAsync_When_Api_Returns_Null_Should_Return_Null()
        {
            var deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            deviceApi.Setup(x => x.SearchByEuiAsync(It.IsNotNull<DevEui>()))
                .ReturnsAsync((SearchDevicesResult)null);

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            var deviceFactory = new TestLoRaDeviceFactory(deviceClient.Object, DeviceCache);

            using var deviceRegistry = new LoRaDeviceRegistry(
                ServerConfiguration,
                this.cache,
                deviceApi.Object,
                deviceFactory,
                DeviceCache);

            var actual = await deviceRegistry.GetDeviceByDevEUIAsync(new DevEui(1).ToString("N", null));
            Assert.Null(actual);

            deviceApi.VerifyAll();
            deviceClient.VerifyAll();
        }

        [Fact]
        public async Task GetDeviceByDevEUIAsync_When_Api_Returns_Empty_Should_Return_Null()
        {
            var deviceApi = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            deviceApi.Setup(x => x.SearchByEuiAsync(It.IsNotNull<DevEui>()))
                .ReturnsAsync(new SearchDevicesResult());

            var deviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);

            var deviceFactory = new TestLoRaDeviceFactory(deviceClient.Object, DeviceCache);

            using var deviceRegistry = new LoRaDeviceRegistry(
                ServerConfiguration,
                this.cache,
                deviceApi.Object,
                deviceFactory,
                DeviceCache);

            var actual = await deviceRegistry.GetDeviceByDevEUIAsync(new DevEui(1).ToString("N", null));
            Assert.Null(actual);

            deviceApi.VerifyAll();
            deviceClient.VerifyAll();
        }
    }
}
