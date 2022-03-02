// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Message dispatcher.
    /// </summary>
    public sealed class MessageDispatcher : IAsyncDisposable, IMessageDispatcher
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private readonly IJoinRequestMessageHandler joinRequestHandler;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<MessageDispatcher> logger;
        private readonly Histogram<double> d2cMessageDeliveryLatencyHistogram;

        public MessageDispatcher(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry deviceRegistry,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            IJoinRequestMessageHandler joinRequestHandler,
            ILoggerFactory loggerFactory,
            ILogger<MessageDispatcher> logger,
            Meter meter)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            this.deviceRegistry = deviceRegistry;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;

            // Register frame counter initializer
            // It will take care of seeding ABP devices created here for single gateway scenarios
            this.deviceRegistry.RegisterDeviceInitializer(new FrameCounterLoRaDeviceInitializer(configuration.GatewayID, frameCounterUpdateStrategyProvider));

            this.joinRequestHandler = joinRequestHandler;
            this.loggerFactory = loggerFactory;
            this.logger = logger;
            this.d2cMessageDeliveryLatencyHistogram = meter?.CreateHistogram<double>(MetricRegistry.D2CMessageDeliveryLatency);
        }

        /// <summary>
        /// Dispatches a request.
        /// </summary>
        public void DispatchRequest(LoRaRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (request.Payload is null)
            {
                throw new LoRaProcessingException(nameof(request.Payload), LoRaProcessingErrorCode.PayloadNotSet);
            }

            if (request.Region is null)
            {
                throw new LoRaProcessingException(nameof(request.Region), LoRaProcessingErrorCode.RegionNotSet);
            }

            var loggingRequest = new LoggingLoRaRequest(request, this.loggerFactory.CreateLogger<LoggingLoRaRequest>(), this.d2cMessageDeliveryLatencyHistogram);

            if (request.Payload.MessageType == MacMessageType.JoinRequest)
            {
                DispatchLoRaJoinRequest(loggingRequest);
            }
            else if (request.Payload.MessageType is MacMessageType.UnconfirmedDataUp or MacMessageType.ConfirmedDataUp)
            {
                DispatchLoRaDataMessage(loggingRequest);
            }
            else
            {
                this.logger.LogError("Unknwon message type in rxpk, message ignored");
            }
        }

        private void DispatchLoRaJoinRequest(LoggingLoRaRequest request) => this.joinRequestHandler.DispatchRequest(request);

        private void DispatchLoRaDataMessage(LoRaRequest request)
        {
            var loRaPayload = (LoRaPayloadData)request.Payload;
            using var scope = this.logger.BeginDeviceAddressScope(loRaPayload.DevAddr);
            if (!IsValidNetId(loRaPayload.DevAddr))
            {
                this.logger.LogDebug($"device is using another network id, ignoring this message (network: {this.configuration.NetId}, devAddr: {loRaPayload.DevAddr.NetworkId})");
                request.NotifyFailed(LoRaDeviceRequestFailedReason.InvalidNetId);
                return;
            }

            this.deviceRegistry.GetLoRaRequestQueue(request).Queue(request);
        }

        private bool IsValidNetId(DevAddr devAddr)
        {
            // Check if the current dev addr is in our network id
            var devAddrNwkid = devAddr.NetworkId;
            if (devAddrNwkid == this.configuration.NetId.NetworkId)
            {
                return true;
            }

            // If not, check if the devaddr is part of the allowed dev address list
            if (this.configuration.AllowedDevAddresses != null && this.configuration.AllowedDevAddresses.Contains(devAddr))
            {
                return true;
            }

            return false;
        }

        public ValueTask DisposeAsync() => this.deviceRegistry.DisposeAsync();
    }
}
