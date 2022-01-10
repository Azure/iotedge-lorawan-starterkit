// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit.LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit;

    public class FunctionBundlerProviderTests
    {
        [Fact]
        public async Task CreateIfRequired_Result_Has_Correct_Bundler_Request()
        {
            // arrange
            const string gatewayId = "foo";
            const double rssi = 2.3;
            var device = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            using var loRaDevice = TestUtils.CreateFromSimulatedDevice(device, new Mock<ILoRaDeviceClientConnectionManager>().Object);
            var payload = device.CreateConfirmedDataUpMessage("foo");
            using var request = WaitableLoRaRequest.Create(TestUtils.GenerateTestRadioMetadata(rssi: rssi), payload);
            var deviceApiServiceMock = new Mock<LoRaDeviceAPIServiceBase>();
            deviceApiServiceMock.Setup(s => s.ExecuteFunctionBundlerAsync(It.IsAny<string>(), It.IsAny<FunctionBundlerRequest>())).ReturnsAsync(new FunctionBundlerResult());
            var subject = new FunctionBundlerProvider(deviceApiServiceMock.Object, NullLoggerFactory.Instance, NullLogger<FunctionBundlerProvider>.Instance);

            // act
            var bundler = subject.CreateIfRequired(gatewayId, payload, loRaDevice, new Mock<IDeduplicationStrategyFactory>().Object, request);
            _ = await bundler.Execute();

            // assert
            deviceApiServiceMock.Verify(s => s.ExecuteFunctionBundlerAsync(loRaDevice.DevEUI,
                                                                           It.Is<FunctionBundlerRequest>(actual => actual.ClientFCntDown == 0
                                                                                                                   && actual.ClientFCntUp == 1
                                                                                                                   && actual.GatewayId == gatewayId
                                                                                                                   && actual.Rssi == rssi)));
        }
    }
}
