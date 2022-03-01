// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Xunit;
    using Xunit.Abstractions;

    public sealed class DefaultLoRaDataRequestHandlerExceptionTests : MessageProcessorTestBase
    {
        private readonly Mock<TestDefaultLoRaRequestHandler> mockTestDefaultLoRaRequestHandler;
        private readonly TestOutputLoggerFactory testOutputLoggerFactory;

        private TestDefaultLoRaRequestHandler Subject => this.mockTestDefaultLoRaRequestHandler.Object;

        public DefaultLoRaDataRequestHandlerExceptionTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            this.testOutputLoggerFactory = new TestOutputLoggerFactory(testOutputHelper);
            this.mockTestDefaultLoRaRequestHandler = new Mock<TestDefaultLoRaRequestHandler>(MockBehavior.Default,
                ServerConfiguration,
                FrameCounterUpdateStrategyProvider,
                ConcentratorDeduplication,
                PayloadDecoder,
                new DeduplicationStrategyFactory(this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<DeduplicationStrategyFactory>()),
                new LoRaADRStrategyProvider(this.testOutputLoggerFactory),
                new LoRAADRManagerFactory(LoRaDeviceApi.Object, this.testOutputLoggerFactory),
                new FunctionBundlerProvider(LoRaDeviceApi.Object, this.testOutputLoggerFactory, this.testOutputLoggerFactory.CreateLogger<FunctionBundlerProvider>()),
                testOutputHelper)
            { CallBase = true };
            LoRaDeviceApi.Setup(api => api.NextFCntDownAsync(It.IsAny<DevEui>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                         .Returns(Task.FromResult<uint>(1));
        }

        public static TheoryData<Exception?, Exception?, Exception?, Exception> Throws_Main_Processing_Exception_When_Deferred_Tasks_Failed_TheoryData() =>
            TheoryDataFactory.From<Exception?, Exception?, Exception?, Exception>(new (Exception?, Exception?, Exception?, Exception)[]
            {
                (new InvalidOperationException("Main exception"), new OperationCanceledException("Deferred task canceled"), null, new InvalidOperationException("Main exception")),
                (null, new OperationCanceledException("Deferred task canceled"), null, new OperationCanceledException("Deferred task canceled")),
                (null, null, new LoRaProcessingException(), new LoRaProcessingException()),
                (null, new InvalidOperationException("A"), new LoRaProcessingException("B"), new AggregateException(new Exception[] { new InvalidOperationException("A"), new LoRaProcessingException("B") })),
            });

        [Theory]
        [MemberData(nameof(Throws_Main_Processing_Exception_When_Deferred_Tasks_Failed_TheoryData))]
        public async Task Throws_Main_Processing_Exception_When_Deferred_Tasks_Failed(Exception? mainProcessingException, Exception? cloudToDeviceException, Exception? saveChangesException, Exception expected)
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            using var request = CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage("foo"));

            if (mainProcessingException is { } someMainProcessingException)
            {
                this.mockTestDefaultLoRaRequestHandler.Setup(c => c.DownlinkMessageBuilderResponse(It.IsAny<LoRaRequest>(),
                                                                                                   It.IsAny<LoRaDevice>(),
                                                                                                   It.IsAny<LoRaOperationTimeWatcher>(),
                                                                                                   It.IsAny<LoRaADRResult>(),
                                                                                                   It.IsAny<IReceivedLoRaCloudToDeviceMessage>(),
                                                                                                   It.IsAny<uint?>(),
                                                                                                   It.IsAny<bool>()))
                                                      .Throws(someMainProcessingException);
            }

            if (cloudToDeviceException is { } someCloudToDeviceException)
            {
                var receivedCloudToDeviceMessage = new Mock<IReceivedLoRaCloudToDeviceMessage>();
                receivedCloudToDeviceMessage.Setup(m => m.RejectAsync()).ThrowsAsync(someCloudToDeviceException);
                this.mockTestDefaultLoRaRequestHandler.SetupSequence(c => c.ReceiveCloudToDeviceAsync(It.IsAny<LoRaDevice>(), It.IsAny<TimeSpan>()))
                                                      .ReturnsAsync(receivedCloudToDeviceMessage.Object)
                                                      .ReturnsAsync((IReceivedLoRaCloudToDeviceMessage?)null);
            }

            if (saveChangesException is { } someSaveChangesException)
            {
                this.mockTestDefaultLoRaRequestHandler.Setup(h => h.SaveChangesToDeviceAsync(It.IsAny<LoRaDevice>(), It.IsAny<bool>()))
                                                      .ThrowsAsync(someSaveChangesException);
            }

            // act + assert
            var ex = await Assert.ThrowsAsync(expected.GetType(), () => Subject.ProcessRequestAsync(request, CreateLoRaDevice(simulatedDevice)));

            if (ex is AggregateException someAggregateException)
            {
                var expectedAggregateException = Assert.IsType<AggregateException>(expected);
                Assert.Equal(expectedAggregateException.InnerExceptions.Count, someAggregateException.InnerExceptions.Count);
                foreach (var (expectedInner, actualInner) in expectedAggregateException.InnerExceptions.Zip(someAggregateException.InnerExceptions))
                {
                    Assert.Equal(expectedInner.GetType(), actualInner.GetType());
                    Assert.Equal(expectedInner.Message, actualInner.Message);
                }
            }
            else
            {
                Assert.Equal(expected.Message, ex.Message);
            }
        }

        protected override async ValueTask DisposeAsync(bool disposing)
        {
            if (disposing)
            {
                this.testOutputLoggerFactory.Dispose();
            }

            await base.DisposeAsync(disposing);
        }
    }
}
