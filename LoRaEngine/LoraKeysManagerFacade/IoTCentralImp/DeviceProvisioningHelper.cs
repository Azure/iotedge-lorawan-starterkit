// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;
    using Microsoft.Azure.Devices.Provisioning.Client;
    using Microsoft.Azure.Devices.Provisioning.Client.Transport;
    using Microsoft.Azure.Devices.Shared;

    public class DeviceProvisioningHelper : IDeviceProvisioningHelper
    {
        private readonly string deviceProvisioningEndpoint;
        private readonly string provisioningScopeId;
        private readonly string deviceProvisioningPrimaryKey;
        private readonly string deviceProvisioningSecondaryKey;

        public DeviceProvisioningHelper(string provisioningScopeId, string primaryKey, string secondaryKey, string deviceProvisioningEndpoint = "global.azure-devices-provisioning.net")
        {
            this.deviceProvisioningEndpoint = deviceProvisioningEndpoint;
            this.provisioningScopeId = provisioningScopeId;
            this.deviceProvisioningPrimaryKey = primaryKey;
            this.deviceProvisioningSecondaryKey = secondaryKey;
        }

        public SymmetricKeyAttestation ProvisionDevice(string deviceId, out string assignedIoTHubHostname)
        {
            var attestation = this.ComputeAttestation(deviceId);

            using var transportHandler = new ProvisioningTransportHandlerHttp();
            using var securityProvider = new SecurityProviderSymmetricKey(deviceId, primaryKey: attestation.SymmetricKey.PrimaryKey, secondaryKey: attestation.SymmetricKey.SecondaryKey);
            var provisioningClient = ProvisioningDeviceClient.Create(
              this.deviceProvisioningEndpoint,
              this.provisioningScopeId,
              securityProvider,
              transportHandler);

            var registerResult = provisioningClient.RegisterAsync().Result;

            if (registerResult.Status == ProvisioningRegistrationStatusType.Assigned)
            {
                assignedIoTHubHostname = registerResult.AssignedHub;
                return attestation;
            }

            assignedIoTHubHostname = null;

            return null;
        }

        public SymmetricKeyAttestation ComputeAttestation(string deviceId)
        {
            var attestation = new SymmetricKeyAttestation
            {
                Type = "symmetricKey",
                SymmetricKey = new SymmetricKey()
            };

            using (var hmac = new HMACSHA256(Convert.FromBase64String(this.deviceProvisioningPrimaryKey)))
            {
                var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(deviceId));
                attestation.SymmetricKey.PrimaryKey = Convert.ToBase64String(signature);
            }

            using (var hmac = new HMACSHA256(Convert.FromBase64String(this.deviceProvisioningSecondaryKey)))
            {
                var signature = hmac.ComputeHash(Encoding.ASCII.GetBytes(deviceId));
                attestation.SymmetricKey.SecondaryKey = Convert.ToBase64String(signature);
            }

            return attestation;
        }
    }
}
