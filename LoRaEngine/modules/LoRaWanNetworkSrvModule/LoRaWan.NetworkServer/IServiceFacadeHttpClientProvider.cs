//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Net.Http;

namespace LoRaWan.NetworkServer
{
    /// <summary>
    /// Provides a <see cref="HttpClient"/> to access Service Facade API
    /// </summary>
    public interface IServiceFacadeHttpClientProvider
    {
        /// <summary>
        /// Gets the <see cref="HttpClient"/> to access the function
        /// </summary>
        /// <returns></returns>
        HttpClient GetHttpClient();
    }
}