namespace LoRaWan.Tests.Common
{
    using System;
    using System.Collections.Generic;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Primitives;

    public static class HttpRequestHelper
    {
        public static HttpRequest CreateRequest(string method = "GET",
                                                IEnumerable<KeyValuePair<string, StringValues>> headers = null,
                                                IEnumerable<KeyValuePair<string, StringValues>> queryParameters = null)
        {
            var httpRequest = new DefaultHttpContext().Request;
            httpRequest.Method = method;
            foreach (var h in headers ?? Array.Empty<KeyValuePair<string, StringValues>>())
                httpRequest.Headers.Add(h);

            httpRequest.Query = new QueryCollection(new Dictionary<string, StringValues>(queryParameters ?? Array.Empty<KeyValuePair<string, StringValues>>()));
            return httpRequest;
        }
    }
}
