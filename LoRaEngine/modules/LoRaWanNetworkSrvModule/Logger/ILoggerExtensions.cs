// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;

    public static class ILoggerExtensions
    {
        public const string DevEUIKey = "DevEUI";

        public static IDisposable BeginDeviceScope(this ILogger logger, string devEUI) =>
            logger?.BeginScope(new Dictionary<string, object> { [DevEUIKey] = devEUI });
    }
}
