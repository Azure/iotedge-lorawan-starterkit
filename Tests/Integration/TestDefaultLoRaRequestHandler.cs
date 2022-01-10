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
    using Microsoft.Extensions.Logging.Abstractions;

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
            IFunctionBundlerProvider functionBundlerProvider) : base(
                configuration,
                frameCounterUpdateStrategyProvider,
                concentratorDeduplication,
                payloadDecoder,
                deduplicationFactory,
                loRaADRStrategyProvider,
                loRaADRManagerFactory,
                functionBundlerProvider,
                NullLogger<DefaultLoRaDataRequestHandler>.Instance,
                TestMeter.Instance)
        {
            this.configuration = configuration;
        }

        protected override Task<FunctionBundlerResult> TryUseBundler(LoRaRequest request, LoRaDevice loRaDevice, LoRaPayloadData loraPayload, bool useMultipleGateways)
            => Task.FromResult(TryUseBundlerAssert());

        protected override Task<LoRaADRResult> PerformADR(LoRaRequest request, LoRaDevice loRaDevice, LoRaPayloadData loraPayload, uint payloadFcnt, LoRaADRResult loRaADRResult, ILoRaDeviceFrameCounterUpdateStrategy frameCounterStrategy)
            => Task.FromResult(PerformADRAssert());

        protected override Task<IReceivedLoRaCloudToDeviceMessage> ReceiveCloudToDeviceAsync(LoRaDevice loRaDevice, TimeSpan timeAvailableToCheckCloudToDeviceMessages)
            => Task.FromResult<IReceivedLoRaCloudToDeviceMessage>(null);

        protected override Task<bool> SendDeviceEventAsync(LoRaRequest request, LoRaDevice loRaDevice, LoRaOperationTimeWatcher timeWatcher, object decodedValue, bool isDuplicate, byte[] decryptedPayloadData)
            => Task.FromResult(SendDeviceAsyncAssert());

        protected override DownlinkMessageBuilderResponse DownlinkMessageBuilderResponse(LoRaRequest request,
                                                                                         LoRaDevice loRaDevice,
                                                                                         LoRaOperationTimeWatcher timeWatcher,
                                                                                         LoRaADRResult loRaADRResult,
                                                                                         IReceivedLoRaCloudToDeviceMessage cloudToDeviceMessage,
                                                                                         uint? fcntDown,
                                                                                         bool fpending) =>
            DownlinkMessageBuilderResponseAssert(request, loRaDevice, timeWatcher, loRaADRResult, cloudToDeviceMessage, fcntDown, fpending);

        protected override Task SendMessageDownstreamAsync(LoRaRequest request, DownlinkMessageBuilderResponse confirmDownlinkMessageBuilderResp)
            => Task.FromResult(SendMessageDownstreamAsyncAssert(confirmDownlinkMessageBuilderResp));

        protected override Task SaveChangesToDeviceAsync(LoRaDevice loRaDevice, bool stationEuiChanged)
            => Task.FromResult(SaveChangesToDeviceAsyncAssert());

        public virtual LoRaADRResult PerformADRAssert() => null;

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
