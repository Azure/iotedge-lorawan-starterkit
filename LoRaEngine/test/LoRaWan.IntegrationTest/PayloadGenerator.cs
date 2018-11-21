using System;
using System.Threading;

namespace LoRaWan.IntegrationTest
{
    // Paylod generator ensures that sample payloads won't be duplicated in a single test execution
    public static class PayloadGenerator
    {
        static int currentPayload = 100;

        public static int Next()
        {
            return Interlocked.Increment(ref currentPayload);
        }

    }
}