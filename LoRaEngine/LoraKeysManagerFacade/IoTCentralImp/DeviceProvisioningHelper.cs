// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System.Threading.Tasks;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;
    using Microsoft.Azure.Devices.Provisioning.Client;
    using Microsoft.Azure.Devices.Provisioning.Client.Transport;
    using Microsoft.Azure.Devices.Shared;

    public class DeviceProvisioningHelper : IDeviceProvisioningHelper
    {
        private readonly string deviceProvisioningEndpoint;
        private readonly string provisioningScopeId;

        public DeviceProvisioningHelper(string provisioningScopeId, string deviceProvisioningEndpoint = "global.azure-devices-provisioning.net")
        {
            this.deviceProvisioningEndpoint = deviceProvisioningEndpoint;
            this.provisioningScopeId = provisioningScopeId;
        }

        public async Task<bool> ProvisionDeviceAsync(string deviceId, SymmetricKeyAttestation attestation)
        {
            ProvisioningDeviceClient provisioningClient = ProvisioningDeviceClient.Create(
                this.deviceProvisioningEndpoint,
                this.provisioningScopeId,
                new SecurityProviderSymmetricKey(deviceId, attestation.SymmetricKey.PrimaryKey, attestation.SymmetricKey.SecondaryKey),
                new ProvisioningTransportHandlerHttp());

            var registerResult = await provisioningClient.RegisterAsync();

            return registerResult.Status == ProvisioningRegistrationStatusType.Assigned;
        }
    }
}
