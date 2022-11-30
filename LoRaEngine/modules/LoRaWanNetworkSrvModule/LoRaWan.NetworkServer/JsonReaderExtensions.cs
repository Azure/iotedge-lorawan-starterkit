// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

namespace LoRaWan.NetworkServer
{
    using Jacob;

    internal static class JsonReaderExtensions
    {
        public static IJsonReader<T?> OrNull<T>(this IJsonReader<T> reader, T? @null = default)
            where T : struct =>
            JsonReader.Null(@null).Or(from v in reader select (T?)v);

        public static IJsonReader<T?> OrNull<T>(this IJsonReader<T> reader, T? @null = default)
            where T : class =>
            JsonReader.Null(@null).Or(from v in reader select (T?)v);
    }
}
