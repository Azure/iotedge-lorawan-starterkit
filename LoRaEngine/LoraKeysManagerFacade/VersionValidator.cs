// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using LoRaTools.CommonAPI;
    using Microsoft.AspNetCore.Http;
    using System;

    public static class VersionValidator
    {
        public static void Validate(HttpRequest req)
        {
            if (req is null) throw new ArgumentNullException(nameof(req));

            var currentApiVersion = ApiVersion.LatestVersion;
            req.HttpContext.Response.Headers.Add(ApiVersion.HttpHeaderName, currentApiVersion.Version);

            var requestedVersion = req.GetRequestedVersion();
            if (requestedVersion == null || !currentApiVersion.SupportsVersion(requestedVersion))
            {
                throw new IncompatibleVersionException($"Incompatible versions (requested: '{requestedVersion?.Name ?? string.Empty}', current: '{currentApiVersion.Name}')");
            }
        }
    }
}
