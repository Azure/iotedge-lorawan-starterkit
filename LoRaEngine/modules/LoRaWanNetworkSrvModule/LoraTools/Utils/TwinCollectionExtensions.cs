// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using LoRaWan;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class TwinCollectionExtensions
    {
        private static readonly Dictionary<Type, Func<object, object>> customConverters = new Dictionary<Type, Func<object, object>>
        {
            [typeof(StationEui)] = (s) => StationEui.Parse(s.ToString()),
            [typeof(DevNonce)] = (s) => new DevNonce(Convert.ToUInt16(s, CultureInfo.InvariantCulture)),
            [typeof(DevAddr)] = (s) => DevAddr.Parse(s.ToString()),
        };

        public static T? SafeRead<T>(this TwinCollection twinCollection, string property, T? defaultValue = default, ILogger? logger = null)
            => twinCollection.TryRead<T>(property, logger, out var someT) ? someT : defaultValue;

        public static string ReadRequiredString(this TwinCollection twinCollection, string property, ILogger? logger = null) =>
                twinCollection.TryRead<string>(property, logger, out var someString) && !string.IsNullOrEmpty(someString)
                                ? someString
                                : throw new InvalidOperationException($"Property '{property}' does not exist or is empty.");

        public static bool TryRead<T>(this TwinCollection twinCollection, string property, ILogger? logger, [NotNullWhen(true)] out T? value)
        {
            _ = twinCollection ?? throw new ArgumentNullException(nameof(twinCollection));

            value = default;

            if (!twinCollection.Contains(property))
                return false;

            // cast to object to avoid dynamic code to be generated
            var some = (object)twinCollection[property];

            // quick path for values that can be directly converted
            if (some is JValue someJValue)
            {
                if (someJValue.Value is T someT)
                {
                    value = someT;
                    return true;
                }

                if (someJValue.Type == JTokenType.Null)
                {
                    return false;
                }
            }

            try
            {
                var t = typeof(T);

                if (TryGetCustomConverter(t, out var converter))
                {
                    value = (T)converter(some);
                }
                else
                {
                    var someConvertedValue = (T)Convert.ChangeType(some, t, CultureInfo.InvariantCulture);
                    if (t.IsEnum && !t.IsEnumDefined(someConvertedValue))
                    {
                        LogParsingError(logger, property, some);
                        return false;
                    }
                    value = someConvertedValue;
                }
            }
            catch (Exception ex) when (ex is ArgumentException
                                          or InvalidCastException
                                          or FormatException
                                          or OverflowException
                                          or JsonSerializationException)
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

        private static bool TryGetCustomConverter(Type t, [NotNullWhen(true)] out Func<object, object>? converter)
            => customConverters.TryGetValue(t, out converter);

        private static void LogParsingError(ILogger? logger, string property, object? value, Exception? ex = default)
            => logger?.LogError(ex, "Failed to parse twin '{TwinProperty}'. The value stored is '{TwinValue}'", property, value);
    }
}
