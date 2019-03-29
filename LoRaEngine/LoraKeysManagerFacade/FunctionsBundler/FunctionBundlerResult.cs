// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.FunctionBundler
{
    using LoRaTools.ADR;

    public class FunctionBundlerResult
    {
        public DuplicateMsgResult DeduplicationResult { get; set; }

        public LoRaADRResult AdrResult { get; set; }

        public uint? NextFCntDown { get; set; }

        public PreferredGatewayResult PreferredGatewayResult { get; set; }
    }
}
