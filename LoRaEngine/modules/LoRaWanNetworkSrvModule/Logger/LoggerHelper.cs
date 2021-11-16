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
        /// Right now we only support DevEUI, but could be extended to
        /// include others (StationEUI, Gateway, DevAddr etc).
        /// If the message is already prefixed with the DevEUI, we don't
        /// add it again.
        /// </summary>
        /// <param name="message">The already formatted message</param>
        /// <returns></returns>
        public static string AddScopeInformation(IExternalScopeProvider? scopeProvider, string message)
        {
            if (scopeProvider is { } sp)
            {
                sp.ForEachScope<object?>((activeScope, _) =>
                {
                    if (activeScope is IDictionary<string, object> activeScopeDictionary &&
                        activeScopeDictionary.TryGetValue(ILoggerExtensions.DevEUIKey, out var obj) &&
                        obj is string devEUI &&
                        !message.StartsWith(devEUI, StringComparison.OrdinalIgnoreCase))
                    {
                        message = string.Concat(devEUI, " ", message);
                    }
                }, null);
            }

            return message;
        }
    }
}
