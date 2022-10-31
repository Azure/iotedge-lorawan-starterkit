// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tools.CLI.Tests.Unit
{
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class JsonUtil
    {
        /// <summary>
        /// Takes somewhat non-conforming JSON
        /// (<a href="https://github.com/JamesNK/Newtonsoft.Json/issues/646#issuecomment-356194475">as accepted by Json.NET</a>)
        /// text and re-formats it to be strictly conforming to RFC 7159.
        /// </summary>
        /// <remarks>
        /// This is a helper primarily designed to make it easier to express JSON as C# literals in
        /// inline data for theory tests, where the double quotes don't have to be escaped.
        /// </remarks>
        public static string Strictify(string json) =>
            JToken.Parse(json).ToString(Formatting.None);
    }
}
