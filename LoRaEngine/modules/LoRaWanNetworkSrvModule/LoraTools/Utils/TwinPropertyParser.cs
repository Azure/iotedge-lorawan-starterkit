// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Utils
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using LoRaWan;
    using Microsoft.Extensions.Logging;

    internal static class TwinPropertyParser
    {
        private static readonly Type StationEuiType = typeof(StationEui);
        private static readonly Type DevNonceType = typeof(DevNonce);
        private static readonly Type DevAddrType = typeof(DevAddr);
        private static readonly Type AppSessionKeyType = typeof(AppSessionKey);
        private static readonly Type AppKeyType = typeof(AppKey);
        private static readonly Type NetworkSessionKeyType = typeof(NetworkSessionKey);
        private static readonly Type JoinEuiType = typeof(JoinEui);
        private static readonly Type NetIdType = typeof(NetId);

        public static bool TryParse<T>(string property, object some, ILogger? logger, [NotNullWhen(true)] out T? value)
        {
            try
            {
                if (!Parse(some, out value))
                {
                    LogParsingError(logger, property, value);
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is ArgumentException
                              or InvalidCastException
                              or FormatException
                              or OverflowException
                              or Newtonsoft.Json.JsonSerializationException)
            {
                LogParsingError(logger, property, some);
                value = default;

                return false;
            }
        }

        public static bool Parse<T>(object some, [NotNullWhen(true)] out T? value)
        {
            // quick path for values that can be directly converted
            if (some is Newtonsoft.Json.Linq.JValue someJValue && someJValue.Value is T someT)
            {
                value = someT;
                return true;
            }

            var t = typeof(T);

            var tPrime = Nullable.GetUnderlyingType(t) ?? t;

            // For 100% case coverage we should handle the case where type T is nullable and the token is null.
            // Since this is not possible in IoT hub, we do not handle the null cases exhaustively.

            if (tPrime == StationEuiType)
                value = (T)(object)StationEui.Parse(some.ToString());
            else if (tPrime == DevNonceType)
                value = (T)(object)new DevNonce(Convert.ToUInt16(some, CultureInfo.InvariantCulture));
            else if (tPrime == DevAddrType)
                value = (T)(object)DevAddr.Parse(some.ToString());
            else if (tPrime == AppSessionKeyType)
                value = (T)(object)AppSessionKey.Parse(some.ToString());
            else if (tPrime == AppKeyType)
                value = (T)(object)AppKey.Parse(some.ToString());
            else if (tPrime == NetworkSessionKeyType)
                value = (T)(object)NetworkSessionKey.Parse(some.ToString());
            else if (tPrime == JoinEuiType)
                value = (T)(object)JoinEui.Parse(some.ToString());
            else if (tPrime == NetIdType)
                value = (T)(object)NetId.Parse(some.ToString());
            else
                value = (T)Convert.ChangeType(some, t, CultureInfo.InvariantCulture);

            return !t.IsEnum || t.IsEnumDefined(value);
        }

        private static void LogParsingError(ILogger? logger, string property, object? value, Exception? ex = default)
            => logger?.LogError(ex, "Failed to parse twin '{TwinProperty}'. The value stored is '{TwinValue}'", property, value);
    }
}
