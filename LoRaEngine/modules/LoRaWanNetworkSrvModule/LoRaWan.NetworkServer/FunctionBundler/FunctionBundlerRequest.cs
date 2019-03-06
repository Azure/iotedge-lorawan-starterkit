// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer
{
    using LoRaTools.CommonAPI;
    using LoRaWan.NetworkServer.ADR;

    public class FunctionBundlerRequest
    {
        public string GatewayId { get; set; }

        public int ClientFCntUp { get; set; }

        public int ClientFCntDown { get; set; }

        public LoRaADRRequest AdrRequest { get; set; }

        public FunctionBundlerItemType FunctionItems { get; set; }
    }
}
