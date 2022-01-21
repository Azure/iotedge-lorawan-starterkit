// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;
    using LoRaWan.Tests.Common;
    using Microsoft.Azure.Devices.Client;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public class DeduplicationStrategyIntegrationTests : MessageProcessorMultipleGatewayBase
    {
        private readonly ITestOutputHelper testOutputHelper;

        public DeduplicationStrategyIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData(DeduplicationMode.Mark)]
        [InlineData(DeduplicationMode.Drop)]
        [InlineData(DeduplicationMode.None)]
        public async Task Validate_Dup_Message_Processing(DeduplicationMode mode)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var messageProcessed = false;

            foreach (var api in new[] { LoRaDeviceApi, SecondLoRaDeviceApi })
            {
                api.Setup(x => x.ExecuteFunctionBundlerAsync(simulatedDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                   .Returns((DevEui _, FunctionBundlerRequest _) =>
                   {
                       var isDup = messageProcessed;
                       messageProcessed = true;
                       return Task.FromResult(new FunctionBundlerResult()
                       {
                           DeduplicationResult = new DeduplicationResult
                           {
                               IsDuplicate = isDup
                           }
                       });
                   });

                api.Setup(x => x.NextFCntDownAsync(It.IsAny<DevEui>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                   // this call should only be made, if we do not have a deduplication strategy
                   // since otherwise we expect the fcntDown to be calculated in the same API call as deduplication
                   .ReturnsAsync(() => mode == DeduplicationMode.None ? simulatedDevice.FrmCntDown + 1 : throw new InvalidOperationException());

                api.Setup(x => x.ABPFcntCacheResetAsync(It.IsAny<DevEui>(), It.IsAny<uint>(), It.IsNotNull<string>()))
                   .ReturnsAsync(true);
            }

            var actualDeviceTelemetries = new List<LoRaDeviceTelemetry>();
            LoRaDeviceClient
                .Setup(x => x.SendEventAsync(It.IsNotNull<LoRaDeviceTelemetry>(), null))
                .ReturnsAsync(true)
                .Callback<LoRaDeviceTelemetry, Dictionary<string, string>>((telemetry, dict) => actualDeviceTelemetries.Add(telemetry));

            LoRaDeviceClient
                .Setup(x => x.ReceiveAsync(It.IsAny<TimeSpan>()))
                .ReturnsAsync((Message)null);

            await SendTwoMessages(mode);

            var actualCounts =
                (NullCount: actualDeviceTelemetries.Count(t => t.DupMsg == null),
                 TrueCount: actualDeviceTelemetries.Count(t => t.DupMsg is { } someDup),
                 FalseCount: actualDeviceTelemetries.Count(t => t.DupMsg is { } someDup && !someDup));

            var expectedCounts = mode switch
            {
                DeduplicationMode.Mark => (1, 1, 0),
                DeduplicationMode.Drop => (1, 0, 0),
                DeduplicationMode.None => (2, 0, 0),
                _ => throw new NotImplementedException()
            };

            Assert.Equal(expectedCounts, actualCounts);
        }

        private async Task SendTwoMessages(DeduplicationMode mode)
        {
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
            var (messageProcessor1, dispose1) = CreateMessageDispatcher(ServerConfiguration, FrameCounterUpdateStrategyProvider, LoRaDeviceApi.Object);
            var (messageProcessor2, dispose2) = CreateMessageDispatcher(SecondServerConfiguration, SecondFrameCounterUpdateStrategyProvider, SecondLoRaDeviceApi.Object);
            using (messageProcessor1)
            using (dispose1)
            using (messageProcessor2)
            using (dispose2)
            {
                var payload = simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 1);

                using var request1 = CreateWaitableRequest(payload);
                using var request2 = CreateWaitableRequest(payload);

                messageProcessor1.DispatchRequest(request1);
                messageProcessor2.DispatchRequest(request2);

                await Task.WhenAll(request1.WaitCompleteAsync(Timeout.Infinite), request2.WaitCompleteAsync(Timeout.Infinite));

                switch (mode)
                {
                    case DeduplicationMode.Drop:
                    {
#pragma warning disable IDE0072 // Add missing cases
                        var (succeededRequest, failedRequest) = request1.ProcessingSucceeded switch
#pragma warning restore IDE0072 // Add missing cases
                        {
                            true => (request1, request2),
                            false => (request2, request1)
                        };
                        
                        Assert.True(succeededRequest.ProcessingSucceeded);
                        Assert.True(failedRequest.ProcessingFailed);
                        Assert.Equal(LoRaDeviceRequestFailedReason.DeduplicationDrop, failedRequest.ProcessingFailedReason);
                        break;
                    }
                    case DeduplicationMode.Mark:
                        Assert.True(request1.ProcessingSucceeded);
                        Assert.True(request2.ProcessingSucceeded);
                        break;
                    case DeduplicationMode.None:
                    default:
                        break;
                }
            }

            (MessageDispatcher, IDisposable) CreateMessageDispatcher(NetworkServerConfiguration networkServerConfiguration,
                                                                     ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
                                                                     LoRaDeviceAPIServiceBase loRaDeviceApi)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope (ownership transferred to caller)
                var cache = EmptyMemoryCache();
                var connectionManager = new LoRaDeviceClientConnectionManager(cache, new TestOutputLogger<LoRaDeviceClientConnectionManager>(this.testOutputHelper));
                var concentratorDeduplication = new ConcentratorDeduplication(cache, new TestOutputLogger<IConcentratorDeduplication>(this.testOutputHelper));
                var requestHandler = CreateDefaultLoRaDataRequestHandler(networkServerConfiguration, frameCounterUpdateStrategyProvider, loRaDeviceApi, concentratorDeduplication);
                var loRaDevice = TestUtils.CreateFromSimulatedDevice(simulatedDevice, connectionManager, requestHandler);
                loRaDevice.Deduplication = mode;
                connectionManager.Register(loRaDevice, LoRaDeviceClient.Object);
                var loraDeviceCache = CreateDeviceCache(loRaDevice);
                var loraDeviceFactory = new TestLoRaDeviceFactory(networkServerConfiguration, LoRaDeviceClient.Object, connectionManager, loraDeviceCache, requestHandler);
                var loRaDeviceRegistry = new LoRaDeviceRegistry(networkServerConfiguration, cache, loRaDeviceApi, loraDeviceFactory, loraDeviceCache);
                return (new MessageDispatcher(networkServerConfiguration, loRaDeviceRegistry, frameCounterUpdateStrategyProvider),
                        new DisposableHolder(() =>
#pragma warning restore CA2000 // Dispose objects before losing scope
                        {
                            cache.Dispose();
                            loraDeviceCache.Dispose();
                            loRaDeviceRegistry.Dispose();
                            connectionManager.Dispose();
                        }));
            }
        }

        private sealed class DisposableHolder : IDisposable
        {
            private readonly Action dispose;

            public DisposableHolder(Action dispose) => this.dispose = dispose;

            public void Dispose() => this.dispose();
        }
    }
}
