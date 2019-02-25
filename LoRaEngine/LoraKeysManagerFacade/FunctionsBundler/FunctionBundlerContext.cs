// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    public class FunctionBundlerContext
    {
        public FunctionBundlerContext(DuplicateMsgCacheCheck dupMsgCheckFunction, LoRaADRFunction loRaADRFunction, FCntCacheCheck fCntCacheCheckFunction)
        {
            this.DupMsgCheckFunction = dupMsgCheckFunction;
            this.LoRaADRFunction = loRaADRFunction;
            this.FCntCacheCheckFunction = fCntCacheCheckFunction;
        }

        public DuplicateMsgCacheCheck DupMsgCheckFunction { get; private set; }

        public LoRaADRFunction LoRaADRFunction { get; private set; }

        public FCntCacheCheck FCntCacheCheckFunction { get; private set; }
    }
}
