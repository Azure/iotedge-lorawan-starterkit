// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Regions
{
    using System;

    /// <summary>
    /// Represents a Region that has potential dwell time limitations.
    /// Such a region should be understood as an "effective" region that applies to a single concentrator rather than a singleton.
    /// </summary>
    public abstract class DwellTimeLimitedRegion : Region
    {
        private DwellTimeSetting? desiredDwellTimeSetting;
        public DwellTimeSetting DesiredDwellTimeSetting
        {
            get => this.desiredDwellTimeSetting ?? throw new InvalidOperationException("DefaultDwellTimeSetting is null.");
            set => this.desiredDwellTimeSetting = value;
        }

        protected DwellTimeLimitedRegion(LoRaRegionType loRaRegionType)
            : base(loRaRegionType)
        { }

        protected abstract DwellTimeSetting DefaultDwellTimeSetting { get; }

        /// <summary>
        /// Mutates the Region to use the specific dwell time settings.
        /// </summary>
        /// <param name="dwellTimeSetting">Dwell time setting to use.</param>
        public abstract void UseDwellTimeSetting(DwellTimeSetting dwellTimeSetting);
    }
}
