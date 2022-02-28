// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class DeviceLoaderSynchronizerTest : IAsyncDisposable
    {
        private readonly MemoryCache memoryCache;
        private readonly LoRaDeviceClientConnectionManager connectionManager;

        public DeviceLoaderSynchronizerTest()
        {
            this.memoryCache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.memoryCache, NullLogger<LoRaDeviceClientConnectionManager>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await this.connectionManager.DisposeAsync();
            this.memoryCache.Dispose();
        }

        [Fact]
        public async Task When_Device_Api_Throws_Error_Should_Not_Create_Any_Device()
        {
            var devAddr = new DevAddr(0x039090);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsAny<DevAddr>()))
                .Throws(new InvalidOperationException());

            var deviceFactory = new Mock<ILoRaDeviceFactory>(MockBehavior.Strict);

            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            var target = new DeviceLoaderSynchronizer(
                devAddr,
                apiService.Object,
                deviceFactory.Object,
                new NetworkServerConfiguration(),
                deviceCache,
                null,
                NullLogger<DeviceLoaderSynchronizer>.Instance);

            var ex = await Assert.ThrowsAsync<LoRaProcessingException>(async () => await target.LoadAsync());
            Assert.IsType<InvalidOperationException>(ex.InnerException);

            Assert.Equal(0, deviceCache.CalculateStatistics().Count);

            // Device was searched by DevAddr
            apiService.VerifyAll();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1000)]
        public async Task When_Device_Does_Not_Exist_Should_Complete_Requests_As_Failed(int loadDevicesDurationInMs)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var devAddr = simulatedDevice.DevAddr.Value;
            var payload1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var searchMock = apiService.Setup(x => x.SearchByDevAddrAsync(devAddr));
            if (loadDevicesDurationInMs > 0)
            {
                searchMock.ReturnsAsync(new SearchDevicesResult(), TimeSpan.FromMilliseconds(loadDevicesDurationInMs));
            }
            else
            {
                searchMock.ReturnsAsync(new SearchDevicesResult());
            }

            var deviceFactory = new Mock<ILoRaDeviceFactory>(MockBehavior.Strict);

            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            var target = new DeviceLoaderSynchronizer(
                devAddr,
                apiService.Object,
                deviceFactory.Object,
                new NetworkServerConfiguration(),
                deviceCache,
                null,
                NullLogger<DeviceLoaderSynchronizer>.Instance);

            _ = target.LoadAsync();

            using var req1 = WaitableLoRaRequest.Create(payload1);
            target.Queue(req1);

            Assert.True(await req1.WaitCompleteAsync());
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr, req1.ProcessingFailedReason);

            var payload2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2");
            using var req2 = WaitableLoRaRequest.Create(payload2);
            target.Queue(req2);

            Assert.True(await req2.WaitCompleteAsync());
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr, req2.ProcessingFailedReason);

            // Device was searched by DevAddr
            apiService.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Does_Not_Match_Gateway_After_Loading_Should_Fail_Request()
        {
            const string gatewayId = "a_different_one";
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: gatewayId));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DevEui, "pk")
            {
                GatewayId = gatewayId,
                NwkSKey = simulatedDevice.NwkSKey.Value
            };
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsAny<DevAddr>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            var deviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object, deviceCache, this.connectionManager);
            var target = new DeviceLoaderSynchronizer(
                simulatedDevice.DevAddr.Value,
                apiService.Object,
                deviceFactory,
                new NetworkServerConfiguration(),
                deviceCache,
                null,
                NullLogger<DeviceLoaderSynchronizer>.Instance);

            _ = target.LoadAsync();

            using var request = WaitableLoRaRequest.Create(payload);
            target.Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request.ProcessingFailedReason);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device is cached but not initialized
            Assert.True(deviceCache.HasRegistrations(simulatedDevice.DevAddr.Value));
            loRaDeviceClient.Verify(x => x.GetTwinAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task When_Device_Does_Not_Match_Gateway_Should_Fail_Request()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "a_different_one"));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DevEui, "pk")
            {
                GatewayId = "a_different_one",
                NwkSKey = simulatedDevice.NwkSKey.Value
            };

            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsAny<DevAddr>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);

            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            var deviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object, deviceCache, this.connectionManager);
            var target = new DeviceLoaderSynchronizer(
                simulatedDevice.DevAddr.Value,
                apiService.Object,
                deviceFactory,
                new NetworkServerConfiguration(),
                deviceCache,
                null,
                NullLogger<DeviceLoaderSynchronizer>.Instance);

            _ = target.LoadAsync();

            using var request = WaitableLoRaRequest.Create(payload);
            target.Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request.ProcessingFailedReason);

            // device should not be initialized, since it belongs to another gateway
            loRaDeviceClient.Verify(x => x.GetTwinAsync(CancellationToken.None), Times.Never());

            // device should be disconnected after initialization
            loRaDeviceClient.Verify(x => x.DisconnectAsync(CancellationToken.None), Times.Once);

            // Device was searched by DevAddr
            apiService.VerifyAll();

            // Device was created by factory
            loRaDeviceClient.VerifyAll();
        }

        [Theory]
        [InlineData("test-gateway")]
        [InlineData(null)]
        public async Task When_Device_Is_Not_In_Cache_And_Found_In_Api_Does_Not_Match_Mic_Should_Fail_Request(string deviceGatewayID)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: deviceGatewayID));

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DevEui, "pk");
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsAny<DevAddr>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            // Will get device twin
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(simulatedDevice.GetDefaultAbpTwin());

            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            var deviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object, deviceCache, this.connectionManager);
            var target = new DeviceLoaderSynchronizer(
                simulatedDevice.DevAddr.Value,
                apiService.Object,
                deviceFactory,
                new NetworkServerConfiguration(),
                deviceCache,
                null,
                NullLogger<DeviceLoaderSynchronizer>.Instance);

            _ = target.LoadAsync();

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", appSKey: simulatedDevice.AppSKey, nwkSKey: TestKeys.CreateNetworkSessionKey(0xEEAAFF));

            using var request = WaitableLoRaRequest.Create(payload);
            target.Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck, request.ProcessingFailedReason);

            // Device was searched by DevAddr
            apiService.VerifyAll();
            loRaDeviceClient.VerifyAll();
        }
    }

    public class DeviceLoaderSynchronizerProcessRequestTest
    {
        private readonly Mock<LoRaDeviceCache> deviceCache;

        public DeviceLoaderSynchronizerProcessRequestTest()
        {
            this.deviceCache = CreateDeviceCacheMock();
        }

        [Fact]
        public void When_Loading_Devices_Failed()
        {
            var loraRequest = CreateVerifyableRequest();
            var sut = CreateDefaultLoader(loraRequest.Object, this.deviceCache.Object);

            sut.UpdateLoadingDevicesFailed(true);
            sut.ExecuteProcessRequest(loraRequest.Object);

            VerifyFailedReason(loraRequest, LoRaDeviceRequestFailedReason.ApplicationError);
        }

        [Fact]
        public void When_No_Devices_Found()
        {
            var loraRequest = CreateVerifyableRequest();
            var sut = CreateDefaultLoader(loraRequest.Object, this.deviceCache.Object);
            sut.ExecuteProcessRequest(loraRequest.Object);

            VerifyFailedReason(loraRequest, LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr);
        }

        [Fact]
        public async Task When_Not_Our_Device_In_Cache()
        {
            var loraRequestMock = CreateVerifyableRequest();
            var request = loraRequestMock.Object;

            var devAddr = request.Payload.DevAddr;
            var loRaDevice = new LoRaDevice(devAddr, default, null) { IsOurDevice = false };
            this.deviceCache.Setup(x => x.TryGetForPayload(request.Payload, out loRaDevice))
                            .Returns(true);
            this.deviceCache.Setup(x => x.HasRegistrations(devAddr))
                            .Returns(true);

            var sut = CreateDefaultLoader(request, this.deviceCache.Object);
            sut.ExecuteProcessRequest(request);

            VerifyFailedReason(loraRequestMock, LoRaDeviceRequestFailedReason.BelongsToAnotherGateway);

            await loRaDevice.DisposeAsync();
        }

        [Theory]
        [InlineData(true, LoRaDeviceRequestFailedReason.BelongsToAnotherGateway)]
        [InlineData(false, LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck)]
        public void When_Mic_Check_Fails(bool hasRegistrationsFromOtherDevices, LoRaDeviceRequestFailedReason reason)
        {
            var loraRequestMock = CreateVerifyableRequest();

            var request = loraRequestMock.Object;

            var devAddr = request.Payload.DevAddr;
            this.deviceCache.Setup(x => x.HasRegistrations(devAddr))
                            .Returns(true);
            this.deviceCache.Setup(x => x.HasRegistrationsForOtherGateways(devAddr))
                            .Returns(hasRegistrationsFromOtherDevices);

            var sut = CreateDefaultLoader(request, this.deviceCache.Object);
            sut.ExecuteProcessRequest(request);

            VerifyFailedReason(loraRequestMock, reason);
        }

        private static Mock<LoRaDeviceCache> CreateDeviceCacheMock() => new Mock<LoRaDeviceCache>(new LoRaDeviceCacheOptions { ValidationInterval = TimeSpan.FromMilliseconds(int.MaxValue) }, new NetworkServerConfiguration(), NullLogger<LoRaDeviceCache>.Instance, TestMeter.Instance);

        private static void VerifyFailedReason(Mock<LoRaRequest> request, LoRaDeviceRequestFailedReason reason) =>
            request.Verify(x => x.NotifyFailed(reason, It.IsAny<Exception>()), Times.Once);

        private static TestDeviceLoaderSynchronizer CreateDefaultLoader(LoRaRequest request, LoRaDeviceCache deviceCache) =>
            new TestDeviceLoaderSynchronizer(Mock.Of<LoRaDeviceAPIServiceBase>(),
                                             Mock.Of<ILoRaDeviceFactory>(),
                                             deviceCache,
                                             request);


        private static Mock<LoRaRequest> CreateVerifyableRequest()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1");
            var loraRequest = new Mock<LoRaRequest>();
            loraRequest.Setup(x => x.Payload).Returns(payload);
            return loraRequest;
        }

        private class TestDeviceLoaderSynchronizer : DeviceLoaderSynchronizer
        {
            internal TestDeviceLoaderSynchronizer(LoRaDeviceAPIServiceBase loRaDeviceAPIService,
                                                  ILoRaDeviceFactory deviceFactory,
                                                  LoRaDeviceCache deviceCache,
                                                  LoRaRequest loRaRequest)
                : base(loRaRequest.Payload.DevAddr, loRaDeviceAPIService, deviceFactory, new NetworkServerConfiguration(), deviceCache, null, NullLogger<DeviceLoaderSynchronizer>.Instance)
            { }

            internal void ExecuteProcessRequest(LoRaRequest request)
            {
                ProcessRequest(request);
            }

            internal void UpdateLoadingDevicesFailed(bool value) => LoadingDevicesFailed = value;
        }
    }

    public class DeviceLoaderSynchronizerCreateDevicesTest
    {
        [Fact]
        public async Task When_Cache_Contains_Join_Device_It_Is_Reloaded()
        {
            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();

            var devEui = new DevEui(0x123);
            var loRaDevice = new Mock<LoRaDevice>(null, devEui, null);
            loRaDevice.Setup(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None))
                .ReturnsAsync(true);
            deviceCache.Register(loRaDevice.Object);

            var deviceLoader = new TestDeviceLoaderSynchronizer(new DevAddr(0), null, null, deviceCache);

            await deviceLoader.ExecuteCreateDevicesAsync(new[] { new IoTHubDeviceInfo { DevAddr = new DevAddr(0x456), DevEUI = devEui } });

            loRaDevice.Verify(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None), Times.Once);
            loRaDevice.Verify(x => x.CloseConnectionAsync(CancellationToken.None, false), Times.Once);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task When_Cache_Contains_Device_With_Outdated_DevAddr_Connection_Is_Closed(bool cachedDevAddrMatchesIoTHubInfo)
        {
            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();

            var devEui = new DevEui(0x123);
            var devAddr = new DevAddr(0x456);
            var loRaDevice = new Mock<LoRaDevice>(devAddr, devEui, null);
            loRaDevice.Setup(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None))
                .ReturnsAsync(true);
            deviceCache.Register(loRaDevice.Object);

            var deviceLoader = new TestDeviceLoaderSynchronizer(new DevAddr(0), null, null, deviceCache);

            await deviceLoader.ExecuteCreateDevicesAsync(new[] { new IoTHubDeviceInfo { DevAddr = cachedDevAddrMatchesIoTHubInfo ? devAddr : new DevAddr(0x789), DevEUI = devEui } });

            loRaDevice.Verify(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None), Times.Never);
            loRaDevice.Verify(x => x.CloseConnectionAsync(CancellationToken.None, false), Times.Once);
        }

        [Fact]
        public async Task When_Reload_Fails_Loader_Updates_State()
        {
            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();

            var devEui = new DevEui(0x123);
            var loRaDevice = new Mock<LoRaDevice>(null, devEui, null);
            loRaDevice.Setup(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None))
                .ThrowsAsync(new LoRaProcessingException(string.Empty, LoRaProcessingErrorCode.DeviceInitializationFailed));
            deviceCache.Register(loRaDevice.Object);

            var deviceLoader = new TestDeviceLoaderSynchronizer(new DevAddr(0), null, null, deviceCache);

            await deviceLoader.ExecuteCreateDevicesAsync(new[] { new IoTHubDeviceInfo { DevAddr = new DevAddr(0x456), DevEUI = devEui } });

            loRaDevice.Verify(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None), Times.Once);
            loRaDevice.Verify(x => x.CloseConnectionAsync(CancellationToken.None, false), Times.Once);

            Assert.True(deviceLoader.HasLoadingDeviceError);
        }

        [Fact]
        public async Task When_Initializer_Fails_Connection_Is_Closed_And_Exception_Thrown()
        {
            await using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();

            // setting up the LoRaDevice
            var devEui = new DevEui(0x123);
            var devAddr = new DevAddr(0x456);
            var loRaDevice = new Mock<LoRaDevice>(devAddr, devEui, null);
            loRaDevice.Setup(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None))
                .ReturnsAsync(true);
            loRaDevice.Object.IsOurDevice = true;

            // setting up the ILoRaDeviceFactory mock so that CreateAndRegister returns above LoRaDevice
            var deviceFactory = new Mock<ILoRaDeviceFactory>();
            deviceFactory.Setup(x => x.CreateAndRegisterAsync(It.IsAny<IoTHubDeviceInfo>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(loRaDevice.Object);

            // setting up one ILoRaDeviceInitializer to throw
            var exceptionThrown = new InvalidOperationException("Mocked exception");
            var failingInitializer = new Mock<ILoRaDeviceInitializer>();
            failingInitializer.Setup(x => x.Initialize(It.IsAny<LoRaDevice>()))
                              .Throws(exceptionThrown);

            var deviceLoader = new TestDeviceLoaderSynchronizer(new DevAddr(0),
                                                                null,
                                                                deviceFactory.Object,
                                                                deviceCache,
                                                                new HashSet<ILoRaDeviceInitializer> { failingInitializer.Object });
            // acting and asserting that the method throws
            var iotHubDevicesInfo = new[] { new IoTHubDeviceInfo { DevAddr = devAddr, DevEUI = devEui } };
            var actualException = await Assert.ThrowsAsync<AggregateException>(() => deviceLoader.ExecuteCreateDevicesAsync(iotHubDevicesInfo));

            // asserting that the inner exception of the aggregate exception is actually what was expected
            Assert.Equal(exceptionThrown, actualException.InnerException);

            // asserting that the connection was closed nevertheless
            loRaDevice.Verify(x => x.CloseConnectionAsync(CancellationToken.None, false), Times.Once);
        }

        private class TestDeviceLoaderSynchronizer : DeviceLoaderSynchronizer
        {
            internal TestDeviceLoaderSynchronizer(DevAddr devAddr,
                                                  LoRaDeviceAPIServiceBase loRaDeviceAPIService,
                                                  ILoRaDeviceFactory deviceFactory,
                                                  LoRaDeviceCache deviceCache = null,
                                                  HashSet<ILoRaDeviceInitializer> initializers = null)
#pragma warning disable CA2000 // ownership transferred
                : base(devAddr, loRaDeviceAPIService, deviceFactory, new NetworkServerConfiguration(), deviceCache ?? LoRaDeviceCacheDefault.CreateDefault(), initializers, NullLogger<DeviceLoaderSynchronizer>.Instance)
#pragma warning restore CA2000
            { }

            internal Task ExecuteCreateDevicesAsync(IReadOnlyList<IoTHubDeviceInfo> devices) => CreateDevicesAsync(devices);
        }
    }
}
