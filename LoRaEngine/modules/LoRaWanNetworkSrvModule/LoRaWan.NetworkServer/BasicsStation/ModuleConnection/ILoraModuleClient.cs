// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.ModuleConnection
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using System;
    using System.Threading.Tasks;

    public interface ILoraModuleClient:IAsyncDisposable
    {
        uint OperationTimeoutInMilliseconds { get; set; }

        public Task CreateFromEnvironmentAsync(ITransportSettings[] settings);

        public ModuleClient GetModuleClient();
        Task<Twin> GetTwinAsync();
        Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertiesUpdate, object p);
        Task SetMethodDefaultHandlerAsync(Func<MethodRequest, object, Task<MethodResponse>> onDirectMethodCalled, object p);
    }
}
