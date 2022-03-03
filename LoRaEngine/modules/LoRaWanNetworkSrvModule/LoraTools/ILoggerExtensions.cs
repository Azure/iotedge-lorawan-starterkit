// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaTools
{
    using System;
    using System.Collections.Generic;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    public static class ILoggerExtensions
    {
        public const string DevEUIKey = "DevEUI";
        public const string DeviceAddressKey = "DevAddr";
        public const string StationEuiKey = "StationEUI";

        public static IDisposable BeginDeviceScope(this ILogger logger, DevEui? devEUI) =>
            devEUI is { } someDevEui
            ? logger?.BeginScope(new Dictionary<string, object> { [DevEUIKey] = someDevEui.ToString() })
            : NoopDisposable.Instance;

        public static IDisposable BeginDeviceAddressScope(this ILogger logger, DevAddr? devAddr) =>
            devAddr is { } someDevAddr ? logger?.BeginDeviceAddressScope(someDevAddr.ToString()) : NoopDisposable.Instance;

        public static IDisposable BeginDeviceAddressScope(this ILogger logger, string deviceAddress) =>
            logger?.BeginScope(new Dictionary<string, object> { [DeviceAddressKey] = deviceAddress });

        public static IDisposable BeginEuiScope(this ILogger logger, StationEui eui) =>
            logger?.BeginScope(new Dictionary<string, object> { [StationEuiKey] = eui.ToString() });
    }
}
