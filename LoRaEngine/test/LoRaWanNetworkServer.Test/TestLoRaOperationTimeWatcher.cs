// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using LoRaTools.Regions;

    /// <summary>
    /// Helper operation timer that returns a constant elapsed time
    /// </summary>
    internal class TestLoRaOperationTimeWatcher : LoRaOperationTimeWatcher
    {
        private TimeSpan constantElapsedTime;

        public TestLoRaOperationTimeWatcher(Region loraRegion, TimeSpan constantElapsedTime)
            : base(loraRegion)
        {
            this.constantElapsedTime = constantElapsedTime;
        }

        /// <summary>
        /// Gets time passed since start
        /// </summary>
        protected internal override TimeSpan GetElapsedTime() => this.constantElapsedTime;
    }
}