// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;

    public class DeviceProvisioningResult
    {
        public string AssignedIoTHubHostname { get; set; }

        public SymmetricKeyAttestation Attestation { get; set; }
    }
}
