// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using Unit = System.ValueTuple;

    internal interface IJsonReader<out T>
    {
        T Read(ref Utf8JsonReader reader);
    }

    internal sealed class JsonProperty<T>
    {
        public JsonProperty(string name, IJsonReader<T> reader, (bool, T) @default = default)
        {
            Name = name;
            Reader = reader;
            Default = @default;
        }

        public string Name { get; }
        public IJsonReader<T> Reader { get; }
        public (bool, T) Default { get; }
    }

    internal static class JsonReader
    {
        public static T Read<T>(this IJsonReader<T> reader, string json) =>
            reader.Read(Encoding.UTF8.GetBytes(json));

        public static T Read<T>(this IJsonReader<T> reader, ReadOnlySpan<byte> utf8JsonTextBytes)
        {
            var utf8Reader = new Utf8JsonReader(utf8JsonTextBytes);
            _ = utf8Reader.Read();
            return reader.Read(ref utf8Reader);
        }

        public static IJsonReader<string> String() =>
            Create((ref Utf8JsonReader reader) =>
            {
                var result = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException();
                _ = reader.Read();
                return result;
            });

        public static IJsonReader<ulong> UInt64() =>
            Create((ref Utf8JsonReader reader) =>
            {
                var result = reader.TokenType == JsonTokenType.Number ? reader.GetUInt64() : throw new JsonException();
                _ = reader.Read();
                return result;
            });

        public static IJsonReader<object> AsObject<T>(this IJsonReader<T> reader) =>
            from v in reader select (object)v;

        public static IJsonReader<T> Either<T>(IJsonReader<T> reader1, IJsonReader<T> reader2) =>
            Create((ref Utf8JsonReader reader) =>
            {
                try
                {
                    var tempReader = reader;
                    var result = reader1.Read(ref tempReader);
                    reader = tempReader;
                    return result;
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or FormatException or OverflowException)
                {
                    return reader2.Read(ref reader);
                }
            });

        public static IJsonReader<(T1, T2, T3)>
            Tuple<T1, T2, T3>(IJsonReader<T1> item1Reader,
                              IJsonReader<T2> item2Reader,
                              IJsonReader<T3> item3Reader) =>
            Create((ref Utf8JsonReader reader) =>
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException();

                _ = reader.Read(); // "["

                var result = (item1Reader.Read(ref reader), item2Reader.Read(ref reader), item3Reader.Read(ref reader));

                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException();

                _ = reader.Read(); // "]"

                return result;
            });

        public static JsonProperty<T> Property<T>(string name, IJsonReader<T> reader, (bool, T) @default = default) =>
            new(name, reader, @default);

        private static readonly JsonProperty<Unit> UnitProperty =
            Property(string.Empty, Create<Unit>(delegate { throw new NotImplementedException(); }), (true, default));

        public static IJsonReader<T> Object<T>(JsonProperty<T> property) =>
            Object(property, UnitProperty, (v, _) => v);

        public static IJsonReader<T>
            Object<T1, T2, T>(JsonProperty<T1> property1,
                              JsonProperty<T2> property2,
                              Func<T1, T2, T> projector) =>
            Create((ref Utf8JsonReader reader) =>
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException();

                _ = reader.Read(); // "{"

                (bool, T1) value1 = default;
                (bool, T2) value2 = default;

                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.ValueTextEquals(property1.Name))
                    {
                        _ = reader.Read();
                        value1 = (true, property1.Reader.Read(ref reader));
                    }
                    else if (reader.ValueTextEquals(property2.Name))
                    {
                        _ = reader.Read();
                        value2 = (true, property2.Reader.Read(ref reader));
                    }
                    else
                    {
                        _ = reader.Read();
                        reader.Skip();
                        _ = reader.Read();
                    }
                }

                _ = reader.Read(); // "}"

                if (value1 is (false, _) && property1.Default is (true, _))
                    value1 = property1.Default;

                if (value2 is (false, _) && property2.Default is (true, _))
                    value2 = property2.Default;

                return (value1, value2) is ((true, var v1), (true, var v2))
                     ? projector(v1, v2)
                     : throw new JsonException();
            });

        public static IJsonReader<T[]> Array<T>(IJsonReader<T> itemReader) =>
            Create((ref Utf8JsonReader reader) =>
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException();

                _ = reader.Read(); // "["

                var list = new List<T>();
                while (reader.TokenType != JsonTokenType.EndArray)
                    list.Add(itemReader.Read(ref reader));

                _ = reader.Read(); // "]"

                return list.ToArray();
            });

        public static IJsonReader<TResult> Select<T, TResult>(this IJsonReader<T> reader, Func<T, TResult> selector) =>
            Create((ref Utf8JsonReader rdr) => selector(reader.Read(ref rdr)));

        internal delegate T ReadHandler<out T>(ref Utf8JsonReader reader);

        private static IJsonReader<T> Create<T>(ReadHandler<T> func) => new DelegatingJsonReader<T>(func);

        private sealed class DelegatingJsonReader<T> : IJsonReader<T>
        {
            private readonly ReadHandler<T> _func;

            public DelegatingJsonReader(ReadHandler<T> func) => _func = func;
            public T Read(ref Utf8JsonReader reader) => _func(ref reader);
        }
    }
}
