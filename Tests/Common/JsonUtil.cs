// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.IO;
    using System.Text;
    using System.Text.Json;

    public static class JsonUtil
    {
        public static string Minify(string json)
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            JsonDocument.Parse(json).WriteTo(writer);
            writer.Flush();
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
