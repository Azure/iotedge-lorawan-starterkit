// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp.Definitions
{
    using System.Collections.Generic;

    public class DeviceCollection
    {
        public string NextLink { get; set; }

        public IEnumerable<Device> Value { get; set; }
    }
}
