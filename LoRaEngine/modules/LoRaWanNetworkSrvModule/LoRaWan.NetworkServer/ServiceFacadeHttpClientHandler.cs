// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Core
{
    using LoRaTools.CommonAPI;
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// <see cref="HttpClientHandler"/> for service facade function API calls.
    /// Adds api-version to request query string and validates response according with <see cref="MinFunctionVersion"/>.
    /// </summary>
    public class ServiceFacadeHttpClientHandler : HttpClientHandler
    {
        /// <summary>
        /// Expected Function version.
        /// </summary>
        public ApiVersion MinFunctionVersion { get; private set; }

        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> next;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="minFunctionVersion"></param>
        public ServiceFacadeHttpClientHandler(ApiVersion minFunctionVersion)
        {
            MinFunctionVersion = minFunctionVersion;
        }

        /// <summary>
        /// Constructor for unit testing, letting us change the call chain, without calling base.
        /// </summary>
        /// <param name="minFunctionVersion"></param>
        /// <param name="next"></param>
        public ServiceFacadeHttpClientHandler(ApiVersion minFunctionVersion, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> next)
        {
            MinFunctionVersion = minFunctionVersion;
            this.next = next;
        }

        /// <summary>
        /// Handlers http request pipeline.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));

            // adds the version to the request
            request.RequestUri = new Uri(string.Concat(request.RequestUri.ToString(), string.IsNullOrEmpty(request.RequestUri.Query) ? "?" : "&", ApiVersion.QueryStringParamName, "=", MinFunctionVersion.Version));

            // use next if one was provided (for unit testing)
            var response = (this.next != null) ? await this.next(request, cancellationToken) : await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var functionVersion = GetFunctionVersion(response);
                if (!functionVersion.SupportsVersion(MinFunctionVersion))
                {
                    var msg = $"Version mismatch (expected: {MinFunctionVersion.Name}, function version: {functionVersion.Name}), ensure you have the latest version deployed";

                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(msg, Encoding.UTF8, "html/text"),
                        ReasonPhrase = msg,
                    };
                }
            }

            return response;
        }

        /// <summary>
        /// Get function version from "api-version"  response header.
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        private static ApiVersion GetFunctionVersion(HttpResponseMessage response)
        {
            // if no version information is available in response header, log error. Validation failed!
            if (!response.Headers.TryGetValues(ApiVersion.HttpHeaderName, out var versionValues) || !versionValues.Any())
            {
                return ApiVersion.DefaultVersion;
            }

            return ApiVersion.Parse(versionValues.FirstOrDefault(), returnAsKnown: true);
        }
    }
}
