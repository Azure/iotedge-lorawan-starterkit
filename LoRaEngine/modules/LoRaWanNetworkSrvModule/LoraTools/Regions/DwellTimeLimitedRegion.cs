// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Regions
{
    /// <summary>
    /// Represents a Region that has potential dwell time limitations.
    /// </summary>
    public abstract class DwellTimeLimitedRegion : Region
    {
        protected DwellTimeLimitedRegion(LoRaRegionType regionEnum) : base(regionEnum)
        { }

        /// <summary>
        /// Mutates the Region to use the specific dwell time settings.
        /// </summary>
        /// <param name="dwellTimeSetting">Dwell time setting to use.</param>
        public abstract void UseDwellTimeSetting(DwellTimeSetting dwellTimeSetting);
    }
}
