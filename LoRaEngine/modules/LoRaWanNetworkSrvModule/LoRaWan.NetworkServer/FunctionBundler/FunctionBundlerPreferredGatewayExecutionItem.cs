// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer.ADR;
    using Microsoft.Extensions.Logging;

    public class FunctionBundlerPreferredGatewayExecutionItem : IFunctionBundlerExecutionItem
    {
        public void Prepare(FunctionBundlerExecutionContext context, FunctionBundlerRequest request)
        {
            request.FunctionItems |= FunctionBundlerItemType.PreferredGateway;

            Logger.Log(context.LoRaDevice.DevEUI, "FunctionBundler ADR request finished preparing.", LogLevel.Debug);
        }

        public void ProcessResult(FunctionBundlerExecutionContext context, FunctionBundlerResult result)
        {
        }

        public bool RequiresExecution(FunctionBundlerExecutionContext context)
        {
            return context.LoRaDevice.ClassType == LoRaDeviceClassType.C && string.IsNullOrEmpty(context.LoRaDevice.GatewayID);
        }
    }
}
