// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Logging;

    public class FunctionBundlerExecutionContext
    {
        public string GatewayId { get; set; }

        public int FCntUp { get; set; }

        public int FCntDown { get; set; }

        public LoRaPayloadData LoRaPayload { get; set; }

        public LoRaDevice LoRaDevice { get; set; }

        public IDeduplicationStrategyFactory DeduplicationFactory { get; set; }

        public LoRaRequest Request { get; set; }
    }
}
