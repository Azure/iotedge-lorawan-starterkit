// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Collections.Generic;

    internal static class LoRaEnumerable
    {
        public static IEnumerable<T> RepeatInfinite<T>(T value)
        {
            while (true)
                yield return value;
        }
    }
}
