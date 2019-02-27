// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using System.Threading.Tasks;
    using LoRaTools.CommonAPI;

    internal interface IPipelineExecutionContext
    {
        string DevEUI { get; }

        FunctionBundlerRequest Request { get; }

        FunctionBundlerResult Result { get; }

        string FunctionAppDirectory { get; }
    }

    /*
     * internal class PipelineExecutionContext
    {
        internal PipelineExecutionContext(string devEui, FunctionBundlerRequest request, string functionAppDirectory)
        {
            this.DevEUI = devEui;
            this.Request = request;
            this.FunctionAppDirectory = functionAppDirectory;
        }

        internal string DevEUI { get; private set; }

        internal FunctionBundlerRequest Request { get; private set; }

        internal FunctionBundlerResult Result { get; private set; } = new FunctionBundlerResult();

        internal string FunctionAppDirectory { get; private set; }
    }
     * //*/
}
