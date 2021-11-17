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
        /// Right now we only support DevEUI and DevAddr, but could be extended to
        /// include others (StationEUI, Gateway, etc).
        /// If the message is already prefixed with the DevEUI/DevAddr, we don't
        /// add it again.
        /// </summary>
        /// <param name="message">The already formatted message</param>
        /// <returns></returns>
        public static string AddScopeInformation(IExternalScopeProvider? scopeProvider, string message)
        {
            if (scopeProvider is { } sp)
            {
                string? devEui = null;
                string? devAddr = null;

                sp.ForEachScope<object?>((activeScope, _) =>
                {
                    if (activeScope is IDictionary<string, object> activeScopeDictionary)
                    {
                        devEui ??= GetScopeIfNotInMessage(activeScopeDictionary, ILoggerExtensions.DevEUIKey, message);
                        devAddr ??= GetScopeIfNotInMessage(activeScopeDictionary, ILoggerExtensions.DeviceAddressKey, message);
                    }
                }, null);

#pragma warning disable format
                return (devEui, devAddr) switch
                {
                    ({ } d, _)  => string.Concat(d, " ", message),
                    (_, { } d)  => string.Concat(d, " ", message),
                    _           => message
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
