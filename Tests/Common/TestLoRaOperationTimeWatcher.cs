// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using LoRaTools.Regions;
    using LoRaWan.NetworkServer;

    /// <summary>
    /// Helper operation timer that returns a constant elapsed time.
    /// </summary>
    public class TestLoRaOperationTimeWatcher : LoRaOperationTimeWatcher
    {
        private readonly IEnumerator<TimeSpan> elapsedTimes;

        public TestLoRaOperationTimeWatcher(Region loraRegion, IEnumerable<TimeSpan> elapsedTimes)
            : base(loraRegion)
        {
            this.elapsedTimes = elapsedTimes.GetEnumerator();
        }

        /// <summary>
        /// Gets time passed since start.
        /// </summary>
        protected internal override TimeSpan GetElapsedTime()
        {
            if (!this.elapsedTimes.MoveNext())
                throw new InvalidOperationException("More elapsed times requested than were set up.");
            return this.elapsedTimes.Current;
        }
    }
}
