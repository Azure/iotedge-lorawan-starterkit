// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Diagnostics.Metrics;

    public sealed class TestMeter
    {
        /// <summary>
        /// Gets a Meter instance that is not being listened on, which ensures that there is no non-deterministic interference on Metrics between tests running in parallel.
        /// </summary>
        public static readonly Meter Instance = new Meter("LoRaWanTest", "0.1");
    }
}
