// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.ModuleConnection
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ILoraModuleClient : IAsyncDisposable
    {
        /// <summary>
        /// Operation Timeout for the module connection.
        /// </summary>
        public TimeSpan OperationTimeout { get; set; }

        public Task CreateFromEnvironmentAsync(ITransportSettings[] settings);

        public ModuleClient GetModuleClient();
        Task<Twin> GetTwinAsync(CancellationToken cancellationToken);
        Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertiesUpdate, object usercontext);
        Task SetMethodDefaultHandlerAsync(MethodCallback onDirectMethodCalled, object usercontext);
    }
}
