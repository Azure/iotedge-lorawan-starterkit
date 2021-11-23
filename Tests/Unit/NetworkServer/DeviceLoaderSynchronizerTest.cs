// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public sealed class DeviceLoaderSynchronizerTest : IDisposable
    {
        private readonly NetworkServerConfiguration serverConfiguration;

        private readonly MemoryCache memoryCache;
        private readonly LoRaDeviceClientConnectionManager connectionManager;

        public DeviceLoaderSynchronizerTest()
        {
            this.serverConfiguration = new NetworkServerConfiguration()
            {
                GatewayID = "test-gateway",
                LogLevel = "Debug",
            };
            this.memoryCache = new MemoryCache(new MemoryCacheOptions());
            this.connectionManager = new LoRaDeviceClientConnectionManager(this.memoryCache);
        }

        public void Dispose()
        {
            this.connectionManager.Dispose();
            this.memoryCache.Dispose();
        }

        [Fact]
        public async Task When_Device_Api_Throws_Error_Should_Not_Create_Any_Device()
        {
            const string devAddr = "039090";

            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>(MockBehavior.Strict);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .Throws(new InvalidOperationException());

            var deviceFactory = new Mock<ILoRaDeviceFactory>(MockBehavior.Strict);

            using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            using var finished = new SemaphoreSlim(0);
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
            var devAddr = simulatedDevice.DevAddr;
            var payload1 = simulatedDevice.CreateUnconfirmedDataUpMessage("1");
            payload1.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

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

            using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
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
            payload2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);
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
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "pk") {
                GatewayId = gatewayId, NwkSKey = simulatedDevice.NwkSKey
            };
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            var deviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object, deviceCache, this.connectionManager);
            var target = new DeviceLoaderSynchronizer(
                simulatedDevice.DevAddr,
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
            Assert.True(deviceCache.HasRegistrations(simulatedDevice.DevAddr));
            loRaDeviceClient.Verify(x => x.GetTwinAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task When_Device_Does_Not_Match_Gateway_Should_Fail_Request()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "a_different_one"));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "pk")
            {
                GatewayId = "a_different_one",
                NwkSKey = simulatedDevice.NwkSKey
            };

            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);

            using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            var deviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object, deviceCache, this.connectionManager);
            var target = new DeviceLoaderSynchronizer(
                simulatedDevice.DevAddr,
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

            // device should not be disconnected (was never connected)
            loRaDeviceClient.Verify(x => x.Disconnect(), Times.Never());

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
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, "pk");
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            // Will get device twin
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Loose);
            loRaDeviceClient.Setup(x => x.GetTwinAsync(CancellationToken.None))
                .ReturnsAsync(TestUtils.CreateABPTwin(simulatedDevice));

            using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();
            var deviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object, deviceCache, this.connectionManager);
            var target = new DeviceLoaderSynchronizer(
                simulatedDevice.DevAddr,
                apiService.Object,
                deviceFactory,
                new NetworkServerConfiguration(),
                deviceCache,
                null,
                NullLogger<DeviceLoaderSynchronizer>.Instance);

            _ = target.LoadAsync();

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, "00000000000000000000000000EEAAFF");

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
            var sut = CreateDefaultLoader(this.deviceCache.Object);

            sut.UpdateLoadingDevicesFailed(true);
            sut.ExecuteProcessRequest(loraRequest.Object);

            VerifyFailedReason(loraRequest, LoRaDeviceRequestFailedReason.ApplicationError);
        }

        [Fact]
        public void When_No_Devices_Found()
        {
            var loraRequest = CreateVerifyableRequest();
            var sut = CreateDefaultLoader(this.deviceCache.Object);
            sut.ExecuteProcessRequest(loraRequest.Object);

            VerifyFailedReason(loraRequest, LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr);
        }

        [Fact]
        public void When_Not_Our_Device_In_Cache()
        {
            var loraRequestMock = CreateVerifyableRequest();
            var request = loraRequestMock.Object;

            var devAddr = ConversionHelper.ByteArrayToString(request.Payload.DevAddr);
            var loRaDevice = new LoRaDevice(devAddr, "", null) { IsOurDevice = false };
            this.deviceCache.Setup(x => x.TryGetForPayload(request.Payload, out loRaDevice))
                            .Returns(true);
            this.deviceCache.Setup(x => x.HasRegistrations(devAddr))
                            .Returns(true);

            var sut = CreateDefaultLoader(this.deviceCache.Object);
            sut.ExecuteProcessRequest(request);

            VerifyFailedReason(loraRequestMock, LoRaDeviceRequestFailedReason.BelongsToAnotherGateway);

            loRaDevice.Dispose();
        }

        [Theory]
        [InlineData(true, LoRaDeviceRequestFailedReason.BelongsToAnotherGateway)]
        [InlineData(false, LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck)]
        public void When_Mic_Check_Fails(bool hasRegistrationsFromOtherDevices, LoRaDeviceRequestFailedReason reason)
        {
            var loraRequestMock = CreateVerifyableRequest();

            var request = loraRequestMock.Object;

            var devAddr = ConversionHelper.ByteArrayToString(request.Payload.DevAddr);
            this.deviceCache.Setup(x => x.HasRegistrations(devAddr))
                            .Returns(true);
            this.deviceCache.Setup(x => x.HasRegistrationsForOtherGateways(devAddr))
                            .Returns(hasRegistrationsFromOtherDevices);

            var sut = CreateDefaultLoader(this.deviceCache.Object);
            sut.ExecuteProcessRequest(request);

            VerifyFailedReason(loraRequestMock, reason);
        }

        private static Mock<LoRaDeviceCache> CreateDeviceCacheMock() => new Mock<LoRaDeviceCache>(new LoRaDeviceCacheOptions { ValidationInterval = TimeSpan.MaxValue }, new NetworkServerConfiguration());

        private static void VerifyFailedReason(Mock<LoRaRequest> request, LoRaDeviceRequestFailedReason reason) =>
            request.Verify(x => x.NotifyFailed(reason, It.IsAny<Exception>()), Times.Once);

        private static TestDeviceLoaderSynchronizer CreateDefaultLoader(LoRaDeviceCache deviceCache) => 
            new TestDeviceLoaderSynchronizer(Mock.Of<LoRaDeviceAPIServiceBase>(),
                                             Mock.Of<ILoRaDeviceFactory>(),
                                             deviceCache);


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
                                                  LoRaDeviceCache deviceCache)
#pragma warning disable CA2000 // ownership transferred
                : base(null, loRaDeviceAPIService, deviceFactory, new NetworkServerConfiguration(), deviceCache, null)
#pragma warning restore CA2000
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
            using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();

            const string devEUI = "123";
            var loRaDevice = new Mock<LoRaDevice>(null, devEUI, null);
            loRaDevice.Setup(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None))
                .ReturnsAsync(true);
            deviceCache.Register(loRaDevice.Object);

            var deviceLoader = new TestDeviceLoaderSynchronizer(null, null, null, deviceCache);

            await deviceLoader.ExecuteCreateDevicesAsync(new List<IoTHubDeviceInfo> { new IoTHubDeviceInfo { DevAddr = "456",  DevEUI = devEUI} });

            loRaDevice.Verify(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task When_Reload_Fails_Loader_Updates_State()
        {
            using var deviceCache = LoRaDeviceCacheDefault.CreateDefault();

            const string devEUI = "123";
            var loRaDevice = new Mock<LoRaDevice>(null, devEUI, null);
            loRaDevice.Setup(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None))
                .ThrowsAsync(new LoRaProcessingException(string.Empty, LoRaProcessingErrorCode.DeviceInitializationFailed));
            deviceCache.Register(loRaDevice.Object);

            var deviceLoader = new TestDeviceLoaderSynchronizer(null, null, null, deviceCache);

            await deviceLoader.ExecuteCreateDevicesAsync(new List<IoTHubDeviceInfo> { new IoTHubDeviceInfo { DevAddr = "456", DevEUI = devEUI } });

            loRaDevice.Verify(x => x.InitializeAsync(It.IsAny<NetworkServerConfiguration>(), CancellationToken.None), Times.Once);

            Assert.True(deviceLoader.HasLoadingDeviceError);
        }

        private class TestDeviceLoaderSynchronizer : DeviceLoaderSynchronizer
        {
            internal TestDeviceLoaderSynchronizer(string devAddr,
                                                  LoRaDeviceAPIServiceBase loRaDeviceAPIService,
                                                  ILoRaDeviceFactory deviceFactory,
                                                  LoRaDeviceCache deviceCache = null,
                                                  HashSet<ILoRaDeviceInitializer> initializers = null)
#pragma warning disable CA2000 // ownership transferred
                : base(devAddr, loRaDeviceAPIService, deviceFactory, new NetworkServerConfiguration(), deviceCache ?? LoRaDeviceCacheDefault.CreateDefault(), initializers)
#pragma warning restore CA2000
            { }

            internal Task ExecuteCreateDevicesAsync(IReadOnlyList<IoTHubDeviceInfo> devices) => CreateDevicesAsync(devices);
        }
    }
}
