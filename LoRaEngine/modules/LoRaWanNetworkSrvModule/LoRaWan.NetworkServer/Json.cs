namespace LoRaWan.NetworkServer
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    internal static class Json
    {
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
