// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;

    public class DeviceLoaderSynchronizerTest
    {
        private readonly NetworkServerConfiguration serverConfiguration;

        public DeviceLoaderSynchronizerTest()
        {
            this.serverConfiguration = new NetworkServerConfiguration()
            {
                GatewayID = "test-gateway",
                LogLevel = "Debug",
            };

            Logger.Init(new LoggerConfiguration()
            {
                LogLevel = LogLevel.Debug,
            });
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
                .Throws(new Exception());

            var deviceFactory = new Mock<ILoRaDeviceFactory>(MockBehavior.Strict);

            var destinationDictionary = new DevEUIToLoRaDeviceDictionary();
            var finished = new SemaphoreSlim(0);
            var target = new DeviceLoaderSynchronizer(
                devAddr,
                apiService.Object,
                deviceFactory.Object,
                destinationDictionary,
                null,
                this.serverConfiguration,
                (_, l) => { finished.Release(); },
                (d) => { destinationDictionary.TryAdd(d.DevEUI, d); });

            await finished.WaitAsync();

            Assert.Empty(destinationDictionary);

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

            var destinationDictionary = new DevEUIToLoRaDeviceDictionary();
            var finished = new SemaphoreSlim(0);
            var target = new DeviceLoaderSynchronizer(
                devAddr,
                apiService.Object,
                deviceFactory.Object,
                destinationDictionary,
                null,
                this.serverConfiguration,
                (_, l) => { finished.Release(); },
                (d) => destinationDictionary.TryAdd(d.DevEUI, d));

            var req1 = new WaitableLoRaRequest(payload1);
            target.Queue(req1);

            await finished.WaitAsync();

            Assert.True(await req1.WaitCompleteAsync());
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr, req1.ProcessingFailedReason);

            var payload2 = simulatedDevice.CreateUnconfirmedDataUpMessage("2");
            payload2.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);
            var req2 = new WaitableLoRaRequest(payload2);
            target.Queue(req2);

            Assert.True(await req2.WaitCompleteAsync());
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByDevAddr, req2.ProcessingFailedReason);

            // Device was searched by DevAddr
            apiService.VerifyAll();
        }

        [Fact]
        public async Task When_Device_Does_Not_Match_Gateway_Should_Fail_Request()
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1, gatewayID: "a_different_one"));
            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey);

            var apiService = new Mock<LoRaDeviceAPIServiceBase>();
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            // device will be initialized
            loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(TestUtils.CreateABPTwin(simulatedDevice));
            // device will be disconnected
            loRaDeviceClient.Setup(x => x.Disconnect())
                .Returns(true);

            var deviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var destinationDictionary = new DevEUIToLoRaDeviceDictionary();
            var finished = new SemaphoreSlim(0);
            var target = new DeviceLoaderSynchronizer(
                simulatedDevice.DevAddr,
                apiService.Object,
                deviceFactory,
                destinationDictionary,
                null,
                this.serverConfiguration,
                (_, l) => { finished.Release(); },
                (d) => destinationDictionary.TryAdd(d.DevEUI, d));

            var request = new WaitableLoRaRequest(payload);
            target.Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.BelongsToAnotherGateway, request.ProcessingFailedReason);

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
            var iotHubDeviceInfo = new IoTHubDeviceInfo(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, string.Empty);
            apiService.Setup(x => x.SearchByDevAddrAsync(It.IsNotNull<string>()))
                .ReturnsAsync(new SearchDevicesResult(iotHubDeviceInfo.AsList()));

            // Will get device twin
            var loRaDeviceClient = new Mock<ILoRaDeviceClient>(MockBehavior.Strict);
            loRaDeviceClient.Setup(x => x.GetTwinAsync())
                .ReturnsAsync(TestUtils.CreateABPTwin(simulatedDevice));

            var deviceFactory = new TestLoRaDeviceFactory(loRaDeviceClient.Object);

            var destinationDictionary = new DevEUIToLoRaDeviceDictionary();
            var finished = new SemaphoreSlim(0);
            var target = new DeviceLoaderSynchronizer(
                simulatedDevice.DevAddr,
                apiService.Object,
                deviceFactory,
                destinationDictionary,
                null,
                this.serverConfiguration,
                (_, l) => { finished.Release(); },
                (d) => destinationDictionary.TryAdd(d.DevEUI, d));

            var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234");
            payload.SerializeUplink(simulatedDevice.AppSKey, "00000000000000000000000000EEAAFF");

            var request = new WaitableLoRaRequest(payload);
            target.Queue(request);
            Assert.True(await request.WaitCompleteAsync());
            Assert.True(request.ProcessingFailed);
            Assert.Equal(LoRaDeviceRequestFailedReason.NotMatchingDeviceByMicCheck, request.ProcessingFailedReason);

            // Device was searched by DevAddr
            apiService.VerifyAll();
            loRaDeviceClient.VerifyAll();
        }
    }
}
