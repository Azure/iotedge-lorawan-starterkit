// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using System.Net.Http;

    /// <summary>
    /// Provides a <see cref="HttpClient"/> to access Service Facade API
    /// </summary>
    public interface IServiceFacadeHttpClientProvider
    {
        /// <summary>
        /// Gets the <see cref="HttpClient"/> to access the function
        /// </summary>
        HttpClient GetHttpClient();
    }
}