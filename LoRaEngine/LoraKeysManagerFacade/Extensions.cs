// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using Microsoft.Azure.Devices.Shared;

    public static class Extensions
    {
        /// <summary>
        /// Gets the twin property if exists, return string.Empty if not found
        /// </summary>
        public static string GetTwinPropertyStringSafe(this TwinCollection twin, string propertyName)
        {
            return (twin != null && twin.Contains(propertyName)) ? twin[propertyName].Value as string ?? string.Empty : string.Empty;
        }
    }
}