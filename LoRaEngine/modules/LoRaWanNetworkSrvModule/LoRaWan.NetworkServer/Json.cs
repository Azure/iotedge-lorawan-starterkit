// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    internal static class Json
    {
        public delegate T Reader<out T>(Utf8JsonReader reader);

        public static T Read<T>(string json, Reader<T> reader) =>
            Read(Encoding.UTF8.GetBytes(json), reader);

        public static T Read<T>(ReadOnlySpan<byte> buffer, Reader<T> reader)
        {
            var jsonReader = new Utf8JsonReader(buffer);
            _ = jsonReader.Read();
            return reader(jsonReader);
        }

        public static string Stringify(Action<Utf8JsonWriter> writer) =>
            Encoding.UTF8.GetString(Write(writer));

        public static byte[] Write(Action<Utf8JsonWriter> writer)
        {
            using var ms = new MemoryStream();
            using var jsonWriter = new Utf8JsonWriter(ms);
            writer(jsonWriter);
            jsonWriter.Flush();
            return ms.ToArray();
        }
    }
}
