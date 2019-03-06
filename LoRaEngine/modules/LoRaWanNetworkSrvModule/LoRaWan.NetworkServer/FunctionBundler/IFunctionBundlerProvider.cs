// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer.ADR;

    public interface IFunctionBundlerProvider
    {
        FunctionBundler CreateIfRequired(string gatewayId, LoRaPayloadData loRaPayload, LoRaDevice loRaDevice, IDeduplicationStrategyFactory deduplicationFactory, LoRaRequest request);
    }
}
