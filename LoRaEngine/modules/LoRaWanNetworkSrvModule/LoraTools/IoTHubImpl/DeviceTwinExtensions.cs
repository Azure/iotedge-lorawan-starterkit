// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools.IoTHubImpl
{
    using System;
    using Microsoft.Azure.Devices.Shared;

    internal static class DeviceTwinExtensions
    {
        internal static Twin ToIoTHubDeviceTwin(this IDeviceTwin twin)
        {
            ArgumentNullException.ThrowIfNull(twin, nameof(twin));

            if (twin is not IoTHubDeviceTwin iotHubDeviceTwin)
            {
                throw new ArgumentException($"Cannot convert {twin.GetType().Name} to IoTHubDeviceTwin instance.");
            }

            return iotHubDeviceTwin.TwinInstance;
        }
    }
}
