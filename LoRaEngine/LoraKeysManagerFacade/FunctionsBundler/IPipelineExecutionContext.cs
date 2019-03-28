// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Collections.Generic;
    using LoRaTools.CommonAPI;
    using Microsoft.Extensions.Logging;

    public interface IPipelineExecutionContext
    {
        string DevEUI { get; }

        FunctionBundlerRequest Request { get; }

        FunctionBundlerResult Result { get; }

        ILogger Logger { get; }
    }
}
