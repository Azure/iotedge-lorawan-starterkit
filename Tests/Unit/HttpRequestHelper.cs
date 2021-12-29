// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using global::LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;

    public static class HttpRequestHelper
    {
        /// <summary>
        /// Creates an HttpRequest with specified body using the latest API version.
        /// </summary>
        public static HttpRequest CreateRequest(string body)
        {
            var request = new DefaultHttpContext().Request;
            request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            request.Query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
                {
                    { ApiVersion.QueryStringParamName, ApiVersion.LatestVersion.Name }
                });
            return request;
        }
    }
}
