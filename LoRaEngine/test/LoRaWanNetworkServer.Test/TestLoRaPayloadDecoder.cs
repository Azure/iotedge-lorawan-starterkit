// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Threading.Tasks;
    using LoRaTools.LoRaPhysical;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Logging;
    using Moq;

    public class TestLoRaPayloadDecoder : ILoRaPayloadDecoder
    {
        private ILoRaPayloadDecoder decoder;

        public TestLoRaPayloadDecoder(ILoRaPayloadDecoder decoder)
        {
            this.decoder = decoder;
        }

        public void SetDecoder(ILoRaPayloadDecoder decoder) => this.decoder = decoder;

        public ValueTask<DecodePayloadResult> DecodeMessageAsync(string devEUI, byte[] payload, byte fport, string sensorDecoder) => this.decoder.DecodeMessageAsync(devEUI, payload, fport, sensorDecoder);
    }
}
