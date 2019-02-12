// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Message dispatcher
    /// </summary>
    public class MessageDispatcher
    {
        private readonly NetworkServerConfiguration configuration;
        private readonly ILoRaDeviceRegistry deviceRegistry;
        private readonly ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider;
        private volatile Region loraRegion;
        private JoinRequestMessageHandler joinRequestHandler;

        public MessageDispatcher(
            NetworkServerConfiguration configuration,
            ILoRaDeviceRegistry deviceRegistry,
            ILoRaDeviceFrameCounterUpdateStrategyProvider frameCounterUpdateStrategyProvider,
            JoinRequestMessageHandler joinRequestHandler = null)
        {
            this.configuration = configuration;
            this.deviceRegistry = deviceRegistry;
            this.frameCounterUpdateStrategyProvider = frameCounterUpdateStrategyProvider;

            // Register frame counter initializer
            // It will take care of seeding ABP devices created here for single gateway scenarios
            this.deviceRegistry.RegisterDeviceInitializer(new FrameCounterLoRaDeviceInitializer(configuration.GatewayID, frameCounterUpdateStrategyProvider));

            this.joinRequestHandler = joinRequestHandler ?? new JoinRequestMessageHandler(this.configuration, this.deviceRegistry);
        }

        /// <summary>
        /// Dispatches a request
        /// </summary>
        public void DispatchRequest(LoRaRequest request)
        {
            if (!LoRaPayload.TryCreateLoRaPayload(request.Rxpk, out LoRaPayload loRaPayload))
            {
                Logger.Log("There was a problem in decoding the Rxpk", LogLevel.Error);
                request.NotifyFailed(null, LoRaDeviceRequestFailedReason.InvalidRxpk);
                return;
            }

            if (this.loraRegion == null)
            {
                if (!RegionFactory.TryResolveRegion(request.Rxpk))
                {
                    // log is generated in Region factory
                    // move here once V2 goes GA
                    request.NotifyFailed(null, LoRaDeviceRequestFailedReason.InvalidRegion);
                    return;
                }

                this.loraRegion = RegionFactory.CurrentRegion;
            }

            request.SetPayload(loRaPayload);
            request.SetRegion(this.loraRegion);

            var loggingRequest = new LoggingLoRaRequest(request);

            if (loRaPayload.LoRaMessageType == LoRaMessageType.JoinRequest)
            {
                this.DispatchLoRaJoinRequest(loggingRequest);
            }
            else if (loRaPayload.LoRaMessageType == LoRaMessageType.UnconfirmedDataUp || loRaPayload.LoRaMessageType == LoRaMessageType.ConfirmedDataUp)
            {
                this.DispatchLoRaDataMessage(loggingRequest);
            }
            else
            {
                Logger.Log("Unknwon message type in rxpk, message ignored", LogLevel.Error);
            }
        }

        private void DispatchLoRaJoinRequest(LoggingLoRaRequest request) => this.joinRequestHandler.DispatchRequest(request);

        void DispatchLoRaDataMessage(LoRaRequest request)
        {
            var loRaPayload = (LoRaPayloadData)request.Payload;
            if (!this.IsValidNetId(loRaPayload.GetDevAddrNetID(), this.configuration.NetId))
            {
                Logger.Log(ConversionHelper.ByteArrayToString(loRaPayload.DevAddr), $"device is using another network id, ignoring this message (network: {this.configuration.NetId}, devAddr: {loRaPayload.GetDevAddrNetID()})", LogLevel.Debug);
                request.NotifyFailed(null, LoRaDeviceRequestFailedReason.InvalidNetId);
                return;
            }

            this.deviceRegistry.GetLoRaRequestQueue(request).Queue(request);
        }

        bool IsValidNetId(byte devAddrNwkid, uint netId)
        {
            var netIdBytes = BitConverter.GetBytes(netId);
            devAddrNwkid = (byte)(devAddrNwkid >> 1);
            return devAddrNwkid == (netIdBytes[0] & 0b01111111);
        }
    }
}
