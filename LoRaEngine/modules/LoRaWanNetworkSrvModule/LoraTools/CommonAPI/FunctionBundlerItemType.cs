// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.CommonAPI
{
    using System;

    [Flags]
    public enum FunctionBundlerItemType
    {
        FCntDown = 0x1,
        Deduplication = 0x2,
        ADR = 0x4,
        PreferredGateway = 0x8,
        ResetDeviceCache = 0x10
    }
}
