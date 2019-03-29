// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    public class ServiceClientAdapter : IServiceClient
    {
        private readonly ServiceClient serviceClient;

        public ServiceClientAdapter(ServiceClient serviceClient)
        {
            this.serviceClient = serviceClient ?? throw new System.ArgumentNullException(nameof(serviceClient));
        }

        public Task<CloudToDeviceMethodResult> InvokeDeviceMethodAsync(string deviceId, string moduleId, CloudToDeviceMethod cloudToDeviceMethod) => this.serviceClient.InvokeDeviceMethodAsync(deviceId, moduleId, cloudToDeviceMethod);

        public Task SendAsync(string deviceId, Message message) => this.serviceClient.SendAsync(deviceId, message);
    }
}