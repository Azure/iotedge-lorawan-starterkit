// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;

    /// <summary>
    /// Message dispatcher.
    /// </summary>
    public sealed class MessageDispatcher : IDisposable, IMessageDispatcher
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private volatile Region loraRegion;
        private readonly IJoinRequestMessageHandler joinRequestHandler;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<MessageDispatcher> logger;

        public MessageDispatcher(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry deviceRegistry,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            IJoinRequestMessageHandler joinRequestHandler,
            ILoggerFactory loggerFactory,
            ILogger<MessageDispatcher> logger)
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
        }

        /// <summary>
        /// Use this constructor only for tests.
        /// </summary>
        internal MessageDispatcher(NetworkServerConfiguration configuration,
                                   ILoRaDeviceRegistry deviceRegistry,
                                   ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider)
            : this(configuration, deviceRegistry, frameCounterUpdateStrategyProvider,
                   new JoinRequestMessageHandler(configuration, deviceRegistry, NullLogger<JoinRequestMessageHandler>.Instance),
                   NullLoggerFactory.Instance,
                   NullLogger<MessageDispatcher>.Instance)
        { }

        /// <summary>
        /// Dispatches a request.
        /// </summary>
        public void DispatchRequest(LoRaRequest request)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            if (request.Payload is null)
            {
                // Following code is only needed for PktFwd compatibility.
                // Any LoRaRequest generated from LNS protocol 'updf' or 'jreq' messages already has the payload set.
                if (!LoRaPayload.TryCreateLoRaPayload(request.Rxpk, out var loRaPayload))
                {
                    this.logger.LogError("There was a problem in decoding the Rxpk");
                    request.NotifyFailed(LoRaDeviceRequestFailedReason.InvalidRxpk);
                    return;
                }
                request.SetPayload(loRaPayload);
            }

            if (this.loraRegion == null)
            {
#pragma warning disable CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                if (!RegionManager.TryResolveRegion(request.Rxpk, out var currentRegion))
#pragma warning restore CS0618 // #655 - This Rxpk based implementation will go away as soon as the complete LNS implementation is done
                {
                    // log is generated in Region factory
                    // move here once V2 goes GA
                    request.NotifyFailed(LoRaDeviceRequestFailedReason.InvalidRegion);
                    return;
                }

                this.loraRegion = currentRegion;
            }

            request.SetRegion(this.loraRegion);

            var loggingRequest = new LoggingLoRaRequest(request, this.loggerFactory.CreateLogger<LoggingLoRaRequest>());

            if (request.Payload.LoRaMessageType == LoRaMessageType.JoinRequest)
            {
                DispatchLoRaJoinRequest(loggingRequest);
            }
            else if (request.Payload.LoRaMessageType is LoRaMessageType.UnconfirmedDataUp or LoRaMessageType.ConfirmedDataUp)
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
            using var scope = this.logger.BeginDeviceScope(ConversionHelper.ByteArrayToString(loRaPayload.DevAddr));
            if (!IsValidNetId(loRaPayload))
            {
                this.logger.LogDebug($"device is using another network id, ignoring this message (network: {this.configuration.NetId}, devAddr: {loRaPayload.DevAddrNetID})");
                request.NotifyFailed(LoRaDeviceRequestFailedReason.InvalidNetId);
                return;
            }

            this.deviceRegistry.GetLoRaRequestQueue(request).Queue(request);
        }

        private bool IsValidNetId(LoRaPayloadData loRaPayload)
        {
            // Check if the current dev addr is in our network id
            var devAddrNwkid = loRaPayload.DevAddrNetID;
            var netIdBytes = BitConverter.GetBytes(this.configuration.NetId);
            devAddrNwkid = (byte)(devAddrNwkid >> 1);
            if (devAddrNwkid == (netIdBytes[0] & 0b01111111))
            {
                return true;
            }

            // If not, check if the devaddr is part of the allowed dev address list
            var currentDevAddr = ConversionHelper.ByteArrayToString(loRaPayload.DevAddr);
            if (this.configuration.AllowedDevAddresses != null && this.configuration.AllowedDevAddresses.Contains(currentDevAddr))
            {
                return true;
            }

            return false;
        }

        public void Dispose() => this.deviceRegistry.Dispose();
    }
}
