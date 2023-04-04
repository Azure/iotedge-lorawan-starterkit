// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Utils
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Logging;

    public static class TwinCollectionExtensions
    {
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

            return TwinPropertyParser.TryParse<T>(property, some, logger, out value);
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
    }
}
