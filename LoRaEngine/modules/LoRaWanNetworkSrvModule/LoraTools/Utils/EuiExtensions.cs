// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.Utils
{
    using LoRaWan;

    public static class EuiExtensions
    {
        public static string AsIotHubDeviceId(this DevEui devEui) => devEui.ToHex();
    }
}
