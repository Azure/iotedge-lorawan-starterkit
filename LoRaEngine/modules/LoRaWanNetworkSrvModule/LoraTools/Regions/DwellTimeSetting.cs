// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Regions
{
    public record DwellTimeSetting(bool DownlinkDwellTime, bool UplinkDwellTime, uint MaxEirp);
}
