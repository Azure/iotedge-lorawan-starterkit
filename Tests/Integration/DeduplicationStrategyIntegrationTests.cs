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
        private readonly object functionLock = new object();
        private readonly SimulatedDevice simulatedDevice;

        public DeduplicationStrategyIntegrationTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
            this.simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(1));
        }

        [Theory]
        [InlineData(DeduplicationMode.Mark, false)]
        [InlineData(DeduplicationMode.Drop, false)]
        [InlineData(DeduplicationMode.None, false)]
        [InlineData(DeduplicationMode.Mark, true)]
        [InlineData(DeduplicationMode.Drop, true)]
        [InlineData(DeduplicationMode.None, true)]
        public async Task When_Different_Strategies_Are_Used_Ensures_Correct_Upstream_And_Downstream_Processing(DeduplicationMode mode, bool confirmedMessages)
        {
            var messageProcessed = false;
            var counterFctnDown = 0;

            foreach (var api in new[] { LoRaDeviceApi, SecondLoRaDeviceApi })
            {
                api.Setup(x => x.ExecuteFunctionBundlerAsync(this.simulatedDevice.DevEUI, It.IsNotNull<FunctionBundlerRequest>()))
                   .ReturnsAsync((DevEui _, FunctionBundlerRequest _) =>
                   {
                       if (mode != DeduplicationMode.None)
                       {
                           lock (this.functionLock)
                           {
                               var isDup = messageProcessed;
                               messageProcessed = true;
                               return new FunctionBundlerResult()
                               {
                                   DeduplicationResult = new DeduplicationResult { IsDuplicate = isDup }
                               };
                           }
                       }

                       // when DeduplicationMode is None, the Bundler deduplication is not invoked
                       return new FunctionBundlerResult();
                   });

                if (confirmedMessages)
                {
                    api.Setup(x => x.NextFCntDownAsync(It.IsAny<DevEui>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                       .ReturnsAsync(() =>
                       {
                           lock (this.functionLock)
                           {
                               counterFctnDown++;
                               return counterFctnDown switch
                               {
                                   1 => this.simulatedDevice.FrmCntDown + 1, // the first time the message is encountered, it gets a valid frame counter down
                                   _ => 0                                    // any other time, it does not
                               };
                           }
                       });
                }

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

            await SendTwoMessages(mode, confirmedMessages);

            // upstream
            var actualCounts =
                (NullCount: actualDeviceTelemetries.Count(t => t.DupMsg == null),
                 TrueCount: actualDeviceTelemetries.Count(t => t.DupMsg is { }),
                 FalseCount: actualDeviceTelemetries.Count(t => t.DupMsg is { } someDup && !someDup));

            var expectedCounts = mode switch
            {
                DeduplicationMode.Mark => (1, 1, 0),
                DeduplicationMode.Drop => (1, 0, 0),
                DeduplicationMode.None => (2, 0, 0),
                _ => throw new NotImplementedException()
            };

            Assert.Equal(expectedCounts, actualCounts);

            // downstream
            var sentDownstream = DownstreamMessageSender.DownlinkMessages.Concat(SecondDownstreamMessageSender.DownlinkMessages).ToArray();

            if (confirmedMessages)
                Assert.Single(sentDownstream);
            else
                Assert.Empty(sentDownstream);
        }

        private async Task SendTwoMessages(DeduplicationMode mode, bool confirmedMessages)
        {
            using var messageProcessor1 = CreateMessageDispatcher(ServerConfiguration, FrameCounterUpdateStrategyProvider, LoRaDeviceApi.Object);
            using var messageProcessor2 = CreateMessageDispatcher(SecondServerConfiguration, SecondFrameCounterUpdateStrategyProvider, SecondLoRaDeviceApi.Object);
            {
                var payload = confirmedMessages ? this.simulatedDevice.CreateConfirmedDataUpMessage("1234", fcnt: 1) : this.simulatedDevice.CreateUnconfirmedDataUpMessage("1234", fcnt: 1);

                using var request1 = CreateWaitableRequest(payload);
                using var request2 = CreateWaitableRequest(payload);

                messageProcessor1.Value.DispatchRequest(request1);
                messageProcessor2.Value.DispatchRequest(request2);

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

            DisposableValue<MessageDispatcher> CreateMessageDispatcher(NetworkServerConfiguration networkServerConfiguration,
                                                                       ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
                                                                       LoRaDeviceAPIServiceBase loRaDeviceApi)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope (ownership transferred to caller)
                var cache = EmptyMemoryCache();
                var connectionManager = new LoRaDeviceClientConnectionManager(cache, new TestOutputLogger<LoRaDeviceClientConnectionManager>(this.testOutputHelper));
                var concentratorDeduplication = new ConcentratorDeduplication(cache, new TestOutputLogger<IConcentratorDeduplication>(this.testOutputHelper));
                var requestHandler = CreateDefaultLoRaDataRequestHandler(networkServerConfiguration, frameCounterUpdateStrategyProvider, loRaDeviceApi, concentratorDeduplication);
                var loRaDevice = TestUtils.CreateFromSimulatedDevice(this.simulatedDevice, connectionManager, requestHandler.Value);
                loRaDevice.Deduplication = mode;
                connectionManager.Register(loRaDevice, LoRaDeviceClient.Object);
                var loraDeviceCache = CreateDeviceCache(loRaDevice);
                var loraDeviceFactory = new TestLoRaDeviceFactory(networkServerConfiguration, LoRaDeviceClient.Object, connectionManager, loraDeviceCache, requestHandler.Value);
                var loRaDeviceRegistry = new LoRaDeviceRegistry(networkServerConfiguration, cache, loRaDeviceApi, loraDeviceFactory, loraDeviceCache);
                return new DisposableValue<MessageDispatcher>(
                    TestMessageDispatcher.Create(cache, networkServerConfiguration, loRaDeviceRegistry, frameCounterUpdateStrategyProvider),
                    () =>
#pragma warning restore CA2000 // Dispose objects before losing scope
                    {
                        cache.Dispose();
                        connectionManager.Dispose();
                        loRaDevice.Dispose();
                        loraDeviceCache.Dispose();
                        loRaDeviceRegistry.Dispose();
                        requestHandler.Dispose();
                    });
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
