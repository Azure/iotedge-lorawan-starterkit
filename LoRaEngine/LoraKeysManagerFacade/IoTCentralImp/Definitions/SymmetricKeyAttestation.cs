// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp.Definitions
{
    public class SymmetricKeyAttestation : IAttestation
    {
        public string Type { get; set; }

        public SymmetricKey SymmetricKey { get; set; }
    }
}
