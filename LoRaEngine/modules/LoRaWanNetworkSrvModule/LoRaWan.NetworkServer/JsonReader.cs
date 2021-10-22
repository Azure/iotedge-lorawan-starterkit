// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using Unit = System.ValueTuple;

    internal interface IJsonReader<T>
    {
        (bool, T) TryRead(ref Utf8JsonReader reader);
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
        public static T Read<T>(this IJsonReader<T> reader, ref Utf8JsonReader utf8Reader) =>
            reader.TryRead(ref utf8Reader) is (true, var result) ? result : throw new JsonException();

        public static T Read<T>(this IJsonReader<T> reader, string json) =>
            reader.Read(Encoding.UTF8.GetBytes(json));

        public static T Read<T>(this IJsonReader<T> reader, ReadOnlySpan<byte> utf8JsonTextBytes)
        {
            var utf8Reader = new Utf8JsonReader(utf8JsonTextBytes);
            _ = utf8Reader.Read();
            return reader.Read(ref utf8Reader);
        }

        public static IJsonReader<string> String() => Create((ref Utf8JsonReader reader) =>
        {
            if (reader.TokenType != JsonTokenType.String)
                return default;

            var v = reader.GetString();
            _ = reader.Read();
            return (true, v);
        });

        public static IJsonReader<ulong> UInt64() => Create((ref Utf8JsonReader reader) =>
        {
            if (reader.TokenType != JsonTokenType.Number)
                return default;

            var v = reader.GetUInt64();
            _ = reader.Read();
            return (true, v);
        });

        public static IJsonReader<T> Either<T>(IJsonReader<T> reader1, IJsonReader<T> reader2) =>
            Create((ref Utf8JsonReader reader) =>
            {
                var tempReader = reader;
                var result = reader1.TryRead(ref tempReader) is (true, var a) ? (true, a)
                           : reader2.TryRead(ref tempReader) is (true, var b) ? (true, b)
                           : throw new NotSupportedException();

                if (result is (true, _))
                    reader = tempReader;

                return result;
            });

        public static IJsonReader<(T1, T2, T3)>
            Tuple<T1, T2, T3>(IJsonReader<T1> item1Reader,
                              IJsonReader<T2> item2Reader,
                              IJsonReader<T3> item3Reader) =>
            Create((ref Utf8JsonReader reader) =>
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                    return default;

                var tempReader = reader;
                _ = tempReader.Read();

                (T1, T2, T3) result;
                if (item1Reader.TryRead(ref tempReader) is (true, var item1) &&
                    item2Reader.TryRead(ref tempReader) is (true, var item2) &&
                    item3Reader.TryRead(ref tempReader) is (true, var item3))
                {
                    result = (item1, item2, item3);
                }
                else
                {
                    return default;
                }

                if (tempReader.TokenType != JsonTokenType.EndArray)
                    return default;

                _ = tempReader.Read();
                reader = tempReader;

                return (true, result);
            });

        public static JsonProperty<T> Property<T>(string name, IJsonReader<T> reader, (bool, T) @default = default) =>
            new(name, reader, @default);

        public static readonly JsonProperty<Unit> UnitProperty =
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

                var tempReader = reader;
                _ = tempReader.Read();

                (bool, T1) value1 = default;
                (bool, T2) value2 = default;

                while (tempReader.TokenType != JsonTokenType.EndObject)
                {
                    if (tempReader.ValueTextEquals(property1.Name))
                    {
                        _ = tempReader.Read();
                        value1 = property1.Reader.TryRead(ref tempReader);
                        if (value1 is (false, _))
                            return default;
                    }
                    else if (tempReader.ValueTextEquals(property2.Name))
                    {
                        _ = tempReader.Read();
                        value2 = property2.Reader.TryRead(ref tempReader);
                        if (value2 is (false, _))
                            return default;
                    }
                    else
                    {
                        _ = tempReader.Read();
                        tempReader.Skip();
                        _ = tempReader.Read();
                    }
                }

                _ = tempReader.Read(); // "}"

                if (value1 is (false, _) && property1.Default is (true, _))
                    value1 = property1.Default;

                if (value2 is (false, _) && property2.Default is (true, _))
                    value2 = property2.Default;

                if ((a: value1, b: value2) is ((true, var aa), (true, var bb)))
                {
                    reader = tempReader;
                    return (true, projector(aa, bb));
                }

                return default;
            });

        public static IJsonReader<T[]> Array<T>(IJsonReader<T> itemReader) =>
            Create((ref Utf8JsonReader reader) =>
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                    return default;

                var tempReader = reader;
                _ = tempReader.Read();

                var list = new List<T>();
                while (tempReader.TokenType != JsonTokenType.EndArray)
                {
                    if (itemReader.TryRead(ref tempReader) is (true, var item))
                        list.Add(item);
                    else
                        return default;
                }

                _ = tempReader.Read();

                reader = tempReader;

                return (true, list.ToArray());
            });

        public static IJsonReader<TResult> Select<T, TResult>(this IJsonReader<T> reader, Func<T, TResult> selector) =>
            Create((ref Utf8JsonReader rdr) =>
            {
                var tempReader = rdr;
                if (reader.TryRead(ref tempReader) is (true, var a))
                {
                    rdr = tempReader;
                    return (true, selector(a));
                }
                return default;
            });

        internal delegate ValueTuple<bool, T> ReadHandler<T>(ref Utf8JsonReader reader);

        private static IJsonReader<T> Create<T>(ReadHandler<T> func) => new DelegatingJsonReader<T>(func);

        private sealed class DelegatingJsonReader<T> : IJsonReader<T>
        {
            private readonly ReadHandler<T> _func;

            public DelegatingJsonReader(ReadHandler<T> func) => _func = func;
            public (bool, T) TryRead(ref Utf8JsonReader reader) => _func(ref reader);
        }
    }
}
