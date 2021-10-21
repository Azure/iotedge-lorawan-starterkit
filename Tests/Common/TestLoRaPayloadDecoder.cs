// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Common
{
    using System.Threading.Tasks;
    using LoRaWan.NetworkServer;

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
