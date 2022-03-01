// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.ADR;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer;
    using LoRaWan.NetworkServer.ADR;
    using LoRaWan.Tests.Common;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using Moq;
    using Xunit.Abstractions;

    internal class TestDefaultLoRaRequestHandler : DefaultLoRaDataRequestHandler
    {
        private readonly NetworkServerConfiguration configuration;

        public IReceivedLoRaCloudToDeviceMessage ActualCloudToDeviceMessage { get; private set; }

        public TestDefaultLoRaRequestHandler(
            NetworkServerConfiguration configuration,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            IConcentratorDeduplication concentratorDeduplication,
            ILoRaPayloadDecoder payloadDecoder,
            IDeduplicationStrategyFactory deduplicationFactory,
            ILoRaADRStrategyProvider loRaADRStrategyProvider,
            ILoRAADRManagerFactory loRaADRManagerFactory,
            IFunctionBundlerProvider functionBundlerProvider,
            ITestOutputHelper testOutputHelper) : this(
                configuration,
                frameCounterUpdateStrategyProvider,
                concentratorDeduplication,
                payloadDecoder,
                deduplicationFactory,
                loRaADRStrategyProvider,
                loRaADRManagerFactory,
                functionBundlerProvider,
                new TestOutputLogger<DefaultLoRaDataRequestHandler>(testOutputHelper))
        { }

        public TestDefaultLoRaRequestHandler(
            NetworkServerConfiguration configuration,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            IConcentratorDeduplication concentratorDeduplication,
            ILoRaPayloadDecoder payloadDecoder,
            IDeduplicationStrategyFactory deduplicationFactory,
            ILoRaADRStrategyProvider loRaADRStrategyProvider,
            ILoRAADRManagerFactory loRaADRManagerFactory,
            IFunctionBundlerProvider functionBundlerProvider,
            ILogger<DefaultLoRaDataRequestHandler> logger) : base(
                configuration,
                frameCounterUpdateStrategyProvider,
                concentratorDeduplication,
                payloadDecoder,
                deduplicationFactory,
                loRaADRStrategyProvider,
                loRaADRManagerFactory,
                functionBundlerProvider,
                logger,
                TestMeter.Instance)
        {
            this.configuration = configuration;
        }

        protected override FunctionBundler CreateBundler(LoRaPayloadData loraPayload, LoRaDevice loRaDevice, LoRaRequest request)
            => new Mock<FunctionBundler>().Object;

        protected override Task DelayProcessing()
            => DelayProcessingAssert();

        protected override Task<FunctionBundlerResult> TryUseBundler(FunctionBundler bundler, LoRaDevice loRaDevice)
            => Task.FromResult(TryUseBundlerAssert());

        protected override Task<LoRaADRResult> PerformADR(LoRaRequest request, LoRaDevice loRaDevice, LoRaPayloadData loraPayload, uint payloadFcnt, LoRaADRResult loRaADRResult, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
            => Task.FromResult(PerformADRAssert());

        internal override Task<IReceivedLoRaCloudToDeviceMessage> ReceiveCloudToDeviceAsync(LoRaDevice loRaDevice, TimeSpan timeAvailableToCheckCloudToDeviceMessages)
            => Task.FromResult<IReceivedLoRaCloudToDeviceMessage>(null);

        internal override Task<bool> SendDeviceEventAsync(LoRaRequest request, LoRaDevice loRaDevice, LoRaOperationTimeWatcher timeWatcher, object decodedValue, bool isDuplicate, byte[] decryptedPayloadData)
            => Task.FromResult(SendDeviceAsyncAssert());

        internal override DownlinkMessageBuilderResponse DownlinkMessageBuilderResponse(LoRaRequest request,
                                                                                         LoRaDevice loRaDevice,
                                                                                         LoRaOperationTimeWatcher timeWatcher,
                                                                                         LoRaADRResult loRaADRResult,
                                                                                         IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage,
                                                                                         uint? fcntDown,
                                                                                         bool fpending) =>
            DownlinkMessageBuilderResponseAssert(request, loRaDevice, timeWatcher, loRaADRResult, cloudToDeviceMessage, fcntDown, fpending);

        protected override Task SendMessageDownstreamAsync(LoRaRequest request, DownlinkMessageBuilderResponse confirmDownlinkMessageBuilderResp)
            => Task.FromResult(SendMessageDownstreamAsyncAssert(confirmDownlinkMessageBuilderResp));

        internal override Task SaveChangesToDeviceAsync(LoRaDevice loRaDevice, bool stationEuiChanged)
            => Task.FromResult(SaveChangesToDeviceAsyncAssert());

        public virtual LoRaADRResult PerformADRAssert() => null;

        public virtual Task DelayProcessingAssert() => Task.CompletedTask;

        public virtual FunctionBundlerResult TryUseBundlerAssert() => null;

        public virtual bool SendDeviceAsyncAssert() => true;

        public virtual Task SendMessageDownstreamAsyncAssert(DownlinkMessageBuilderResponse confirmDownlinkMessageBuilderResp) => null;

        public virtual bool SaveChangesToDeviceAsyncAssert() => true;

        public virtual DownlinkMessageBuilderResponse DownlinkMessageBuilderResponseAssert(LoRaRequest request,
                                                                                           LoRaDevice loRaDevice,
                                                                                           LoRaOperationTimeWatcher timeWatcher,
                                                                                           LoRaADRResult loRaADRResult,
                                                                                           IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage,
                                                                                           uint? fcntDown,
                                                                                           bool fpending)
        {
            ActualCloudToDeviceMessage = cloudToDeviceMessage;
            return DownlinkMessageBuilder.CreateDownlinkMessage(this.configuration, loRaDevice, request, timeWatcher,
                                                                cloudToDeviceMessage, fpending, fcntDown ?? 0, loRaADRResult, NullLogger.Instance);
        }
    }
}
