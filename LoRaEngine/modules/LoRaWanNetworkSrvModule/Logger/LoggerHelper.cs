// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace Logger
{
    using System;
    using System.Collections.Generic;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    internal static class LoggerHelper
    {
        /// <summary>
        /// Prefixes the message with predefined scope values.
        /// Right now we only support DevEUI, DevAddr and Station EUI.
        /// If the message is already prefixed with the DevEUI/DevAddr, we don't
        /// add it again. Keep in mind that the scopes have a precedence assigned with them -
        /// if we have multiple active scopes, Dev EUI > Dev Addr > Station EUI.
        /// </summary>
        /// <param name="scopeProvider">The scope provider.</param>
        /// <param name="message">The already formatted message.</param>
        /// <param name="euiSeparator">The separator to append after the scope prefix.</param>
        /// <returns></returns>
        public static string AddScopeInformation(IExternalScopeProvider? scopeProvider, string message, string euiSeparator = "")
        {
            if (scopeProvider is { } sp)
            {
                string? devEui = null;
                string? devAddr = null;
                string? stationEui = null;

                sp.ForEachScope<object?>((activeScope, _) =>
                {
                    if (activeScope is IDictionary<string, object> activeScopeDictionary)
                    {
                        devEui ??= GetScopeIfNotInMessage(activeScopeDictionary, ILoggerExtensions.DevEUIKey, message);
                        devAddr ??= GetScopeIfNotInMessage(activeScopeDictionary, ILoggerExtensions.DeviceAddressKey, message);
                        stationEui ??= GetScopeIfNotInMessage(activeScopeDictionary, ILoggerExtensions.StationEuiKey, message);
                    }
                }, null);

#pragma warning disable format
                return (devEui, devAddr, stationEui) switch
                {
                    ({ } d, _, _)   => string.Concat(d, euiSeparator, " ", message),
                    (_, { } d, _)   => string.Concat(d, euiSeparator, " ", message),
                    (_, _, { } d)   => string.Concat(d, euiSeparator, " ", message),
                    _               => message
                };
#pragma warning restore format
            }

            return message;

            static string? GetScopeIfNotInMessage(IDictionary<string, object> dict, string key, string message) =>
                dict.TryGetValue(key, out var o) &&
                o is string result &&
                !message.StartsWith(result, StringComparison.OrdinalIgnoreCase)
                ? result
                : null;
        }
    }
}
