// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using Microsoft.Azure.Devices.Shared;

    internal static class TwinExtensions
    {
        internal static string GetGatewayID(this Twin twin)
        {
            return twin.Properties.Desired.Contains(LoraKeysManagerFacadeConstants.TwinProperty_GatewayID)
                ? twin.Properties.Desired[LoraKeysManagerFacadeConstants.TwinProperty_GatewayID].Value as string
                : string.Empty;
        }

        internal static string GetNwkSKey(this Twin twin)
        {
            string networkSessionKey = null;
            if (twin.Properties.Desired.Contains(LoraKeysManagerFacadeConstants.TwinProperty_NwkSKey))
            {
                networkSessionKey = twin.Properties.Desired[LoraKeysManagerFacadeConstants.TwinProperty_NwkSKey].Value as string;
            }
            else if (twin.Properties.Reported.Contains(LoraKeysManagerFacadeConstants.TwinProperty_NwkSKey))
            {
                networkSessionKey = twin.Properties.Reported[LoraKeysManagerFacadeConstants.TwinProperty_NwkSKey].Value as string;
            }

            return networkSessionKey;
        }
    }
}
