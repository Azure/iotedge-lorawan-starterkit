// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System;
    using System.Linq;
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Primitives;

    /// <summary>
    /// Http utilities.
    /// </summary>
    public static class HttpUtilities
    {
        /// <summary>
        /// Gets requested <see cref="ApiVersion"/> from a <see cref="HttpRequest"/>.
        /// </summary>
        public static ApiVersion GetRequestedVersion(this HttpRequest req)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            var versionText = req.Query[ApiVersion.QueryStringParamName];
            if (StringValues.IsNullOrEmpty(versionText))
            {
                if (req.Headers.TryGetValue(ApiVersion.HttpHeaderName, out var headerValues))
                {
                    if (headerValues.Any())
                    {
                        versionText = headerValues.First();
                    }
                }
            }

            if (StringValues.IsNullOrEmpty(versionText))
            {
                return ApiVersion.DefaultVersion;
            }

            return ApiVersion.Parse(versionText);
        }

        /// <summary>
        /// Checks if the http status code indicates success.
        /// </summary>
        public static bool IsSuccessStatusCode(int statusCode) => statusCode is >= 200 and <= 299;
    }
}
