// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaTools.Utils
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using Microsoft.Extensions.Logging;

    public static class TwinPropertiesExtensions
    {
        public static T? SafeRead<T>(this ITwinProperties twinCollection, string property, T? defaultValue = default, ILogger? logger = null)
            => twinCollection.TryRead<T>(property, logger, out var someT) ? someT : defaultValue;

        public static bool TryRead<T>(this ITwinProperties twinCollection, string property, ILogger? logger, [NotNullWhen(true)] out T? value)
        {
            _ = twinCollection ?? throw new ArgumentNullException(nameof(twinCollection));

            value = default;

            if (!twinCollection.TryGetValue(property, out var some))
                return false;

            return TwinPropertyParser.TryParse<T>(property, some, logger, out value);
        }

        public static bool TryReadJsonBlock(this ITwinProperties twinCollection, string property, [NotNullWhen(true)] out string? json)
        {
            _ = twinCollection ?? throw new ArgumentNullException(nameof(twinCollection));
            json = null;

            if (!twinCollection.TryGetValue(property, out var some))
                return false;

            json = some.ToString();
            return json != null;
        }
    }
}
