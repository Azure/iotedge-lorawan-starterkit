// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    using System;

    public record DwellTimeSetting(bool DownlinkDwellTime, bool UplinkDwellTime, uint MaxEirp)
    {
        public static DwellTimeSetting GetEffectiveDwellTimeSetting(DwellTimeSetting @default, DwellTimeSetting desired, DwellTimeSetting reported)
        {
            throw new NotImplementedException();
        }
    }
}
