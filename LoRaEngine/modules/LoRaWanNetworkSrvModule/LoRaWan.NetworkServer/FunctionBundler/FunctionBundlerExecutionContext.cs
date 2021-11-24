// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.LoRaMessage;

    public class FunctionBundlerExecutionContext
    {
        public FunctionBundlerExecutionContext(string gatewayId, uint fCntUp, uint fCntDown,
                                               LoRaPayloadData loRaPayload, LoRaDevice loRaDevice,
                                               IDeduplicationStrategyFactory deduplicationFactory, LoRaRequest request)
        {
            GatewayId = gatewayId;
            FCntUp = fCntUp;
            FCntDown = fCntDown;
            LoRaPayload = loRaPayload;
            LoRaDevice = loRaDevice;
            DeduplicationFactory = deduplicationFactory;
            Request = request;
        }

        public string GatewayId { get; }

        public uint FCntUp { get; }

        public uint FCntDown { get; }

        public LoRaPayloadData LoRaPayload { get; }

        public LoRaDevice LoRaDevice { get; }

        public IDeduplicationStrategyFactory DeduplicationFactory { get; }

        public LoRaRequest Request { get; }
    }
}
