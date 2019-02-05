// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Threading;

    // Paylod generator ensures that sample payloads won't be duplicated in a single test execution
    public static class PayloadGenerator
    {
        private static int currentPayload = 100;

        public static int Next()
        {
            return Interlocked.Increment(ref currentPayload);
        }
    }
}