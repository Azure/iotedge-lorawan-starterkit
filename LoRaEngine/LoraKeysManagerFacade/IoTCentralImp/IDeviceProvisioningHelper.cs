// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;

    public interface IDeviceProvisioningHelper
    {
        Task<SymmetricKeyAttestation> ProvisionDeviceAsync(string deviceId);

        SymmetricKeyAttestation ComputeAttestation(string deviceId);
    }
}