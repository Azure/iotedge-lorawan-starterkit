// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoraKeysManagerFacade
{
    public class LoRaADRRequest
    {
        public int DataRate { get; set; }

        public int RequiredSnr { get; set; }

        public int FCntUp { get; set; }

        public int FCntDown { get; set; }

        public string GatewayId { get; set; }
    }
}
