// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using LoRaWan;

    internal static class EuiValidator
    {
        /// <summary>
        /// DevEUI are required to be IEEE EUI-64.
        /// </summary>
        /// <param name="devEui">devEUI to validate.</param>
        internal static bool TryParseAndValidate(string devEui, out DevEui result)
        {
            if (!DevEui.TryParse(devEui, out result))
            {
                return false;
            }

            return result != default;
        }
    }
}
