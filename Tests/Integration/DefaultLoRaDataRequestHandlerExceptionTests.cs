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
        private readonly VerifiableLogger<DefaultLoRaDataRequestHandler> verifiableLogger = new();

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
                this.verifiableLogger)
            { CallBase = true };
            LoRaDeviceApi.Setup(api => api.NextFCntDownAsync(It.IsAny<DevEui>(), It.IsAny<uint>(), It.IsAny<uint>(), It.IsAny<string>()))
                         .Returns(Task.FromResult<uint>(1));
        }

        public static TheoryData<Exception?, Exception?, Exception> Deferred_Task_Exceptions_TheoryData() =>
            TheoryDataFactory.From(new (Exception?, Exception?, Exception)[]
            {
                (new OperationCanceledException("Deferred task canceled"), null, new OperationCanceledException("Deferred task canceled")),
                (null, new LoRaProcessingException(), new LoRaProcessingException()),
                (new InvalidOperationException("A"), new LoRaProcessingException("B"), new AggregateException(new Exception[] { new InvalidOperationException("A"), new LoRaProcessingException("B") })),
            });

        [Theory]
        [MemberData(nameof(Deferred_Task_Exceptions_TheoryData))]
        public async Task Logs_Deferred_Task_Exceptions_Even_If_Main_Processing_Fails(Exception? cloudToDeviceException, Exception? saveChangesException, Exception expected)
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            using var request = CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage("foo"));
            SetupMainProcessingFailure(new InvalidOperationException());

            if (cloudToDeviceException is { } someCloudToDeviceException)
                SetupCloudToDeviceFailure(someCloudToDeviceException);

            if (saveChangesException is { } someSaveChangesException)
                SetupSaveDeviceChangeFailure(someSaveChangesException);

            // act + assert
            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Subject.ProcessRequestAsync(request, CreateLoRaDevice(simulatedDevice)));
            AssertDeferredTaskException(expected);
        }

        [Theory]
        [MemberData(nameof(Deferred_Task_Exceptions_TheoryData))]
        public async Task Logs_Deferred_Task_Exceptions(Exception? cloudToDeviceException, Exception? saveChangesException, Exception expected)
        {
            // arrange
            var simulatedDevice = new SimulatedDevice(TestDeviceInfo.CreateABPDevice(0));
            using var request = CreateWaitableRequest(simulatedDevice.CreateConfirmedDataUpMessage("foo"));

            if (cloudToDeviceException is { } someCloudToDeviceException)
                SetupCloudToDeviceFailure(someCloudToDeviceException);

            if (saveChangesException is { } someSaveChangesException)
                SetupSaveDeviceChangeFailure(someSaveChangesException);

            // act
            _ = await Subject.ProcessRequestAsync(request, CreateLoRaDevice(simulatedDevice));

            // assert
            AssertDeferredTaskException(expected);
        }

        private void AssertDeferredTaskException(Exception expected)
        {
            var ex = Assert.Single(this.verifiableLogger.Logs.Where(l => l.Exception is not null)).Exception;
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
                Assert.Equal(expected.Message, ex!.Message);
            }
        }

        private void SetupMainProcessingFailure(Exception exception)
        {
            this.mockTestDefaultLoRaRequestHandler.Setup(c => c.DownlinkMessageBuilderResponse(It.IsAny<LoRaRequest>(),
                                                                                                   It.IsAny<LoRaDevice>(),
                                                                                                   It.IsAny<LoRaOperationTimeWatcher>(),
                                                                                                   It.IsAny<LoRaADRResult>(),
                                                                                                   It.IsAny<IReceivedLoRaCloudToDeviceMessage>(),
                                                                                                   It.IsAny<uint?>(),
                                                                                                   It.IsAny<bool>()))
                                                  .Throws(exception);
        }

        private void SetupCloudToDeviceFailure(Exception exception)
        {
            var receivedCloudToDeviceMessage = new Mock<IReceivedLoRaCloudToDeviceMessage>();
            receivedCloudToDeviceMessage.Setup(m => m.RejectAsync()).ThrowsAsync(exception);
            this.mockTestDefaultLoRaRequestHandler.SetupSequence(c => c.ReceiveCloudToDeviceAsync(It.IsAny<LoRaDevice>(), It.IsAny<TimeSpan>()))
                                                  .ReturnsAsync(receivedCloudToDeviceMessage.Object)
                                                  .ReturnsAsync((IReceivedLoRaCloudToDeviceMessage?)null);
        }

        private void SetupSaveDeviceChangeFailure(Exception exception)
        {
            this.mockTestDefaultLoRaRequestHandler.Setup(h => h.SaveChangesToDeviceAsync(It.IsAny<LoRaDevice>(), It.IsAny<bool>()))
                                                  .ThrowsAsync(exception);
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
