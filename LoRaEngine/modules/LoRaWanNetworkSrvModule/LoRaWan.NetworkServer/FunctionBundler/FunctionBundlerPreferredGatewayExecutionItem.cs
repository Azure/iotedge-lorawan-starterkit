// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using System;

    public class FunctionBundlerPreferredGatewayExecutionItem : IFunctionBundlerExecutionItem
    {
        public void Prepare(FunctionBundlerExecutionContext context, FunctionBundlerRequest request)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            if (request is null) throw new ArgumentNullException(nameof(request));

            request.FunctionItems |= FunctionBundlerItemType.PreferredGateway;
        }

        public void ProcessResult(FunctionBundlerExecutionContext context, FunctionBundlerResult result)
        {
        }

        public bool RequiresExecution(FunctionBundlerExecutionContext context)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));
            return context.LoRaDevice.ClassType == LoRaDeviceClassType.C && string.IsNullOrEmpty(context.LoRaDevice.GatewayID);
        }
    }
}
