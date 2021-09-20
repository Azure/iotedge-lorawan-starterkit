// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade.IoTCentralImp.Definitions
{
    public class Device
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public bool Simulated { get; set; }

        public bool Provisionned { get; set; }

        public string Template { get; set; }

        public bool Enabled { get; set; }
    }
}
