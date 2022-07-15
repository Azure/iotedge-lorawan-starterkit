// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using LoRaTools;
    using LoRaTools.Utils;
    using Microsoft.Azure.Devices.Shared;

    internal static class TwinExtensions
    {
        internal static string GetGatewayID(this Twin twin)
            => twin.Properties.Desired.TryRead<string>(TwinPropertiesConstants.GatewayID, null, out var someGatewayId)
             ? someGatewayId
             : string.Empty;

        internal static string GetNwkSKey(this Twin twin)
            => twin.Properties.Desired.TryRead(TwinPropertiesConstants.NwkSKey, null, out string nwkSKey)
             ? nwkSKey
             : twin.Properties.Reported.TryRead(TwinPropertiesConstants.NwkSKey, null, out nwkSKey)
                ? nwkSKey
                : null;
    }
}
