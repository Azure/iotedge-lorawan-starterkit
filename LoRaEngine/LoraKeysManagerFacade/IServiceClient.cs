// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices;

    /// <summary>
    /// Interface for service client interation
    /// </summary>
    public interface IServiceClient
    {
        Task<CloudToDeviceMethodResult> InvokeDeviceMethodAsync(string deviceId, string moduleId, CloudToDeviceMethod cloudToDeviceMethod);

        Task SendAsync(string deviceId, Message message);
    }
}