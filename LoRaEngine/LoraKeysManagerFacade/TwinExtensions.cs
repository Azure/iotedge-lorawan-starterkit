// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using LoRaTools.Utils;
    using Microsoft.Azure.Devices.Shared;

    internal static class TwinExtensions
    {
        internal static string GetGatewayID(this Twin twin)
            => twin.Properties.Desired.TryRead<string>(LoraKeysManagerFacadeConstants.TwinProperty_GatewayID, null, out var someGatewayId)
             ? someGatewayId
             : string.Empty;

        internal static string GetNwkSKey(this Twin twin)
            => twin.Properties.Desired.TryRead(LoraKeysManagerFacadeConstants.TwinProperty_NwkSKey, null, out string nwkSKey)
             ? nwkSKey
             : twin.Properties.Reported.TryRead(LoraKeysManagerFacadeConstants.TwinProperty_NwkSKey, null, out nwkSKey)
                ? nwkSKey
                : null;
    }
}
