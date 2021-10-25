// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Text.Json;
    using Unit = System.ValueTuple;

    public interface IJsonReader<out T>
    {
        T Read(ref Utf8JsonReader reader);
    }

    public interface IJsonProperty<out T>
    {
        bool IsMatch(Utf8JsonReader reader);
        IJsonReader<T> Reader { get; }
        bool HasDefaultValue { get; }
        T DefaultValue { get; }
    }

    public static partial class JsonReader
    {
        public static T Read<T>(this IJsonReader<T> reader, string json) =>
            reader.Read(Encoding.UTF8.GetBytes(json));

        public static T Read<T>(this IJsonReader<T> reader, ReadOnlySpan<byte> utf8JsonTextBytes)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            var utf8Reader = new Utf8JsonReader(utf8JsonTextBytes);
            _ = utf8Reader.Read();
            return reader.Read(ref utf8Reader);
        }

#pragma warning disable CA1720 // Identifier contains type name (represents JSON string)
        public static IJsonReader<string> String() =>
#pragma warning restore CA1720 // Identifier contains type name
            Create((ref Utf8JsonReader reader) =>
            {
                var result = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new JsonException();
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

        [DebuggerDisplay("{" + nameof(name) + "}")]
        private sealed class JsonProperty<T> : IJsonProperty<T>
        {
            private readonly string name;

            public JsonProperty(string name, IJsonReader<T> reader, (bool, T) @default = default) =>
                (this.name, Reader, (HasDefaultValue, DefaultValue)) = (name, reader, @default);

            public bool IsMatch(Utf8JsonReader reader) =>
                reader.TokenType != JsonTokenType.PropertyName
                    ? throw new ArgumentException(null, nameof(reader))
                    : reader.ValueTextEquals(this.name);

            public IJsonReader<T> Reader { get; }
            public bool HasDefaultValue { get; }
            public T DefaultValue { get; }
        }

        public static IJsonProperty<T> Property<T>(string name, IJsonReader<T> reader, (bool, T) @default = default) =>
            new JsonProperty<T>(name, reader, @default);

        private sealed class NonProperty : IJsonProperty<Unit>
        {
            public static readonly NonProperty Instance = new();

            private NonProperty() { }

            public bool IsMatch(Utf8JsonReader reader) => false;
            public IJsonReader<Unit> Reader => throw new NotSupportedException();
            public bool HasDefaultValue => true;
            public Unit DefaultValue => default;
        }

#pragma warning disable CA1720 // Identifier contains type name (represent JSON object)

        /// <remarks>
        /// Properties without a default value that are missing from the read JSON object will cause
        /// <see cref="JsonException"/> to be thrown.
        /// </remarks>
        public static IJsonReader<T> Object<T>(IJsonProperty<T> property) =>
            Object(property, NonProperty.Instance, NonProperty.Instance,
                   NonProperty.Instance, NonProperty.Instance, NonProperty.Instance,
                   NonProperty.Instance, NonProperty.Instance, NonProperty.Instance,
                   NonProperty.Instance, NonProperty.Instance, NonProperty.Instance,
                   NonProperty.Instance, NonProperty.Instance, NonProperty.Instance,
                   NonProperty.Instance,
                   (v, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _) => v);

        /// <remarks>
        /// Properties without a default value that are missing from the read JSON object will cause
        /// <see cref="JsonException"/> to be thrown.
        /// </remarks>
        public static IJsonReader<TResult>
            Object<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult>(
                IJsonProperty<T1> property1, IJsonProperty<T2> property2, IJsonProperty<T3> property3,
                IJsonProperty<T4> property4, IJsonProperty<T5> property5, IJsonProperty<T6> property6,
                IJsonProperty<T7> property7, IJsonProperty<T8> property8, IJsonProperty<T9> property9,
                IJsonProperty<T10> property10, IJsonProperty<T11> property11, IJsonProperty<T12> property12,
                IJsonProperty<T13> property13, IJsonProperty<T14> property14, IJsonProperty<T15> property15,
                IJsonProperty<T16> property16,
                Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16, TResult> projector) =>
            Create((ref Utf8JsonReader reader) =>
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                    throw new JsonException();

                _ = reader.Read(); // "{"

                (bool, T1) value1 = default;
                (bool, T2) value2 = default;
                (bool, T3) value3 = default;
                (bool, T4) value4 = default;
                (bool, T5) value5 = default;
                (bool, T6) value6 = default;
                (bool, T7) value7 = default;
                (bool, T8) value8 = default;
                (bool, T9) value9 = default;
                (bool, T10) value10 = default;
                (bool, T11) value11 = default;
                (bool, T12) value12 = default;
                (bool, T13) value13 = default;
                (bool, T14) value14 = default;
                (bool, T15) value15 = default;
                (bool, T16) value16 = default;

                while (reader.TokenType != JsonTokenType.EndObject)
                {
                    if (ReadPropertyValue(property1, ref reader, ref value1)) continue;
                    if (ReadPropertyValue(property2, ref reader, ref value2)) continue;
                    if (ReadPropertyValue(property3, ref reader, ref value3)) continue;
                    if (ReadPropertyValue(property4, ref reader, ref value4)) continue;
                    if (ReadPropertyValue(property5, ref reader, ref value5)) continue;
                    if (ReadPropertyValue(property6, ref reader, ref value6)) continue;
                    if (ReadPropertyValue(property7, ref reader, ref value7)) continue;
                    if (ReadPropertyValue(property8, ref reader, ref value8)) continue;
                    if (ReadPropertyValue(property9, ref reader, ref value9)) continue;
                    if (ReadPropertyValue(property10, ref reader, ref value10)) continue;
                    if (ReadPropertyValue(property11, ref reader, ref value11)) continue;
                    if (ReadPropertyValue(property12, ref reader, ref value12)) continue;
                    if (ReadPropertyValue(property13, ref reader, ref value13)) continue;
                    if (ReadPropertyValue(property14, ref reader, ref value14)) continue;
                    if (ReadPropertyValue(property15, ref reader, ref value15)) continue;
                    if (ReadPropertyValue(property16, ref reader, ref value16)) continue;

                    _ = reader.Read();
                    reader.Skip();
                    _ = reader.Read();

                    static bool ReadPropertyValue<TValue>(IJsonProperty<TValue> property,
                        ref Utf8JsonReader reader,
                        ref (bool, TValue) value)
                    {
                        if (value is (true, _) || !property.IsMatch(reader))
                            return false;

                        _ = reader.Read();
                        value = (true, property.Reader.Read(ref reader));
                        return true;
                    }
                }

                _ = reader.Read(); // "}"

                static void DefaultUnassigned<T>(IJsonProperty<T> property, ref (bool, T) v)
                {
                    if (v is (false, _) && property.HasDefaultValue)
                        v = (true, property.DefaultValue);
                }

                DefaultUnassigned(property1, ref value1);
                DefaultUnassigned(property2, ref value2);
                DefaultUnassigned(property3, ref value3);
                DefaultUnassigned(property4, ref value4);
                DefaultUnassigned(property5, ref value5);
                DefaultUnassigned(property6, ref value6);
                DefaultUnassigned(property7, ref value7);
                DefaultUnassigned(property8, ref value8);
                DefaultUnassigned(property9, ref value9);
                DefaultUnassigned(property10, ref value10);
                DefaultUnassigned(property11, ref value11);
                DefaultUnassigned(property12, ref value12);
                DefaultUnassigned(property13, ref value13);
                DefaultUnassigned(property14, ref value14);
                DefaultUnassigned(property15, ref value15);
                DefaultUnassigned(property16, ref value16);

                return (value1, value2, value3,
                        value4, value5, value6,
                        value7, value8, value9,
                        value10, value11, value12,
                        value13, value14, value15,
                        value16) is ((true, var v1), (true, var v2), (true, var v3),
                                     (true, var v4), (true, var v5), (true, var v6),
                                     (true, var v7), (true, var v8), (true, var v9),
                                     (true, var v10), (true, var v11), (true, var v12),
                                     (true, var v13), (true, var v14), (true, var v15),
                                     (true, var v16))
                     ? projector(v1, v2, v3, v4, v5, v6, v7, v8, v9, v10, v11, v12, v13, v14, v15, v16)
                     : throw new JsonException();
            });

#pragma warning restore CA1720 // Identifier contains type name

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
