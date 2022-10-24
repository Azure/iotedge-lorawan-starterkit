// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    public enum LoRaRegionType
    {
        NotSet,
        EU868,
        US915,
        CN470RP1,
        CN470RP2,
        AS923,
        AU915RP1,
        // Following regions are added in the enum for BasicsStation compatibility
        EU863,
        US902
    }
}
