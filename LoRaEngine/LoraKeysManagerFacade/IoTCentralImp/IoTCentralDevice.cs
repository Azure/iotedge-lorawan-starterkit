// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp
{
    using LoraKeysManagerFacade.IoTCentralImp.Definitions;

    internal class IoTCentralDevice : IDevice
    {
        private readonly Device deviceObject;
        private readonly SymmetricKeyAttestation attestationObject;

        public IoTCentralDevice(Device deviceObject, SymmetricKeyAttestation attestationObject, string assignedIoTHubHostname)
        {
            this.deviceObject = deviceObject;
            this.attestationObject = attestationObject;
            this.AssignedIoTHub = assignedIoTHubHostname;
        }

        public string PrimaryKey => this.attestationObject.SymmetricKey.PrimaryKey;

        public string DeviceId => this.deviceObject.Id;

        public string AssignedIoTHub { get; }
    }
}
