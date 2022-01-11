// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Utils
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Text.Json;
    using LoRaWan;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public static class TwinCollectionExtensions
    {
        private static readonly Type StationEuiType = typeof(StationEui);
        private static readonly Type DevNonceType = typeof(DevNonce);
        private static readonly Type DevAddrType = typeof(DevAddr);
        private static readonly Type AppSessionKeyType = typeof(AppSessionKey);
        private static readonly Type AppKeyType = typeof(AppKey);
        private static readonly Type NetworkSessionKeyType = typeof(NetworkSessionKey);
        private static readonly Type NetIdType = typeof(NetId);

        public static T? SafeRead<T>(this TwinCollection twinCollection, string property, T? defaultValue = default, ILogger? logger = null)
            => twinCollection.TryRead<T>(property, logger, out var someT) ? someT : defaultValue;

        public static T ReadRequired<T>(this TwinCollection twinCollection, string property, ILogger? logger = null) =>
            twinCollection.TryRead(property, logger, out T? result)
                ? result
                : throw new InvalidOperationException($"Property '{property}' does not exist or is invalid.");

        public static bool TryRead<T>(this TwinCollection twinCollection, string property, ILogger? logger, [NotNullWhen(true)] out T? value)
        {
            _ = twinCollection ?? throw new ArgumentNullException(nameof(twinCollection));

            value = default;

            if (!twinCollection.Contains(property))
                return false;

            // cast to object to avoid dynamic code to be generated
            var some = (object)twinCollection[property];

            // quick path for values that can be directly converted
            if (some is Newtonsoft.Json.Linq.JValue someJValue)
            {
                if (someJValue.Value is T someT)
                {
                    value = someT;
                    return true;
                }
            }

            try
            {
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
                else if (tPrime == NetIdType)
                    value = (T)(object)NetId.Parse(some.ToString());
                else
                    value = (T)Convert.ChangeType(some, t, CultureInfo.InvariantCulture);

                if (t.IsEnum && !t.IsEnumDefined(value))
                {
                    LogParsingError(logger, property, some);
                    return false;
                }
            }
            catch (Exception ex) when (ex is ArgumentException
                                          or InvalidCastException
                                          or FormatException
                                          or OverflowException
                                          or Newtonsoft.Json.JsonSerializationException)
            {
                LogParsingError(logger, property, some, ex);
                return false;
            }
            return true;
        }

        public static bool TryReadJsonBlock(this TwinCollection twinCollection, string property, [NotNullWhen(true)] out string? json)
        {
            _ = twinCollection ?? throw new ArgumentNullException(nameof(twinCollection));
            json = null;

            if (!twinCollection.Contains(property))
                return false;

            json = ((object)twinCollection[property]).ToString();
            return json != null;
        }

        public static bool TryParseJson<T>(this TwinCollection twinCollection, string property, ILogger? logger, [NotNullWhen(true)] out T? value)
        {
            value = default;
            if (twinCollection.TryReadJsonBlock(property, out var json))
            {
                try
                {
                    value = JsonSerializer.Deserialize<T>(json);
                }
                catch (JsonException ex)
                {
                    logger?.LogError(ex, $"Failed to parse '{property}'. We expect type '{typeof(T)}'");
                }
            }
            return value != null;
        }

        private static void LogParsingError(ILogger? logger, string property, object? value, Exception? ex = default)
            => logger?.LogError(ex, "Failed to parse twin '{TwinProperty}'. The value stored is '{TwinValue}'", property, value);
    }
}
