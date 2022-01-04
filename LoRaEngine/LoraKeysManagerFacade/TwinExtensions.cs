// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using Microsoft.Azure.Devices.Shared;

    internal static class TwinExtensions
    {
        internal static string GetGatewayID(this Twin twin)
            => GetTwinPropertyStringSafe(twin.Properties.Desired, LoraKeysManagerFacadeConstants.TwinProperty_GatewayID);

        internal static string GetNwkSKey(this Twin twin)
        {
            if (!twin.Properties.Desired.TryReadString(LoraKeysManagerFacadeConstants.TwinProperty_NwkSKey, out var networkSessionKey))
            {
                _ = twin.Properties.Reported.TryReadString(LoraKeysManagerFacadeConstants.TwinProperty_NwkSKey, out networkSessionKey);
            }
            return networkSessionKey;
        }

        /// <summary>
        /// Gets the twin property if exists, return string.Empty if not found.
        /// </summary>
        public static string GetTwinPropertyStringSafe(this TwinCollection twin, string propertyName)
            => TryReadString(twin, propertyName, out var someValue) ? someValue : string.Empty;

        private static bool TryReadString(this TwinCollection twin, string propertyName, out string someValue)
        {
            someValue = default;

            if (twin == null || !twin.Contains(propertyName))
                return false;

            someValue = (string)(object)twin[propertyName];
            return true;
        }

    }
}
