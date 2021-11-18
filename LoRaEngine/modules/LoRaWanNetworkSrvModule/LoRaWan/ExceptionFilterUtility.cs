// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;

    public static class ExceptionFilterUtility
    {
        public static bool True(Action action)
        {
            (action ?? throw new ArgumentNullException(nameof(action)))();
            return true;
        }

        public static bool False(Action action)
        {
            (action ?? throw new ArgumentNullException(nameof(action)))();
            return false;
        }
    }
}
