// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Common
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    public interface IPhysicalClient : IDisposable
    {
        /// <summary>
        /// Run the Physical client.
        /// </summary>
        public Task RunServer();
    }
}
