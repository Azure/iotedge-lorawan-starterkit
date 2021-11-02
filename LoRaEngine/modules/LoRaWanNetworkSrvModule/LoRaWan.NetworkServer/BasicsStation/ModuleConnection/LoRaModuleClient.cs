// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.BasicsStation.ModuleConnection
{
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class LoRaModuleClient : ILoraModuleClient
    {
        private ModuleClient moduleClient;

        public uint OperationTimeoutInMilliseconds { get => this.moduleClient.OperationTimeoutInMilliseconds; set => this.moduleClient.OperationTimeoutInMilliseconds = value; }
        public TimeSpan OperationTimeout { get; set; }

        public async Task CreateFromEnvironmentAsync(ITransportSettings[] settings)
        {
            moduleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleClient != null)
            {
                await this.moduleClient.CloseAsync();
                this.moduleClient.Dispose();
            }
        }

        public ModuleClient GetModuleClient() => this.moduleClient;

        public Task<Twin> GetTwinAsync(CancellationToken cancellationToken)
        {
            return this.moduleClient.GetTwinAsync(cancellationToken);
        }

        public async Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback onDesiredPropertiesUpdate, object p)
        {
            await this.moduleClient.SetDesiredPropertyUpdateCallbackAsync(onDesiredPropertiesUpdate, p);
        }

        public async Task SetMethodDefaultHandlerAsync(MethodCallback onDirectMethodCalled, object p)
        {
            await this.moduleClient.SetMethodDefaultHandlerAsync(onDirectMethodCalled, p);
        }
    }
}
