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

    public sealed class TwinCollectionReader
    {
        private readonly TwinCollection twinCollection;
        private readonly ILogger logger;

        public TwinCollectionReader(TwinCollection twinCollection, ILogger logger)
        {
            this.twinCollection = twinCollection;
            this.logger = logger;
        }
        public T? SafeRead<T>(string property, T? defaultValue = default)
        {
            return TryRead<T>(property, out var someT) ? someT : defaultValue;
        }

        public string ReadRequiredString(string property) =>
                TryRead<string>(property, out var someString) && !string.IsNullOrEmpty(someString)
                    ? someString
                    : throw new InvalidOperationException($"Property '{property}' does not exist or is empty.");

        public bool Contains(string propertyName) =>
            this.twinCollection.Contains(propertyName);

        public bool TryRead<T>(string property, [NotNullWhen(true)] out T? value)
        {
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

                if (TryGetConverter(t, out var converter))
                {
                    value = (T)converter(some);
                }
                else
                {
                    var someConvertedValue = (T)Convert.ChangeType(some, t, CultureInfo.InvariantCulture);
                    if (t.IsEnum && !t.IsEnumDefined(someConvertedValue))
                    {
                        LogParsingError(property, some);
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
                LogParsingError(property, some, ex);
                return false;
            }
            return true;
        }

        private static bool TryGetConverter(Type t, [NotNullWhen(true)] out Func<object, object>? converter)
        {
            return converters.TryGetValue(t, out converter);
        }

        private static readonly Dictionary<Type, Func<object, object>> converters = new Dictionary<Type, Func<object, object>>
        {
            [typeof(StationEui)] = (s) => StationEui.Parse(s.ToString())
        };

        public bool TryReadDevNonce(string property, [NotNullWhen(true)] out DevNonce? result)
        {
            result = default;
            if (!TryRead<ushort>(property, out var someDevNonce))
                return false;

            result = new DevNonce(someDevNonce);
            return true;
        }

        public bool TryReadStationEui(string property, out StationEui result)
        {
            result = default;

            if (TryRead<string>(property, out var someStationEui))
            {
                if (StationEui.TryParse(someStationEui, out var stationEui))
                {
                    result = stationEui;
                    return true;
                }
                LogParsingError(property, someStationEui);
            }
            return false;
        }

        private void LogParsingError(string property, object value, Exception? ex = default)
        {
            this.logger.LogError(ex, "Failed to parse twin '{TwinProperty}'. The value stored is '{TwinValue}'", property, value);
        }
    }
}
