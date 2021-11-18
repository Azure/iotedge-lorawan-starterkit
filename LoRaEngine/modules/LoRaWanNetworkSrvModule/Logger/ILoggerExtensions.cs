// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;

    public static class ILoggerExtensions
    {
        public const string DevEUIKey = "DevEUI";
        public const string DeviceAddressKey = "DevAddr";
        public const string StationEuiKey = "StationEUI";

        public static IDisposable BeginDeviceScope(this ILogger logger, string devEUI) =>
            logger?.BeginScope(new Dictionary<string, object> { [DevEUIKey] = devEUI });

        public static IDisposable BeginDeviceAddressScope(this ILogger logger, DevAddr devAddr) =>
            logger?.BeginDeviceAddressScope(devAddr.ToString());

        public static IDisposable BeginDeviceAddressScope(this ILogger logger, string deviceAddress) =>
            logger?.BeginScope(new Dictionary<string, object> { [DeviceAddressKey] = deviceAddress });

        public static IDisposable BeginEuiScope(this ILogger logger, StationEui eui) =>
            logger?.BeginScope(new Dictionary<string, object> { [StationEuiKey] = eui.ToString("N", null) });
    }
}
