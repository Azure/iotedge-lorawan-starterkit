// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using LoRaTools.LoRaMessage;
    using LoRaTools.Utils;
    using Xunit;

    public class MicTest
    {
        [Fact]
        public void CheckMic()
        {
            LoRaDevice loRaDevice = new LoRaDevice("0376F531", "QDH1dgOAoRIGdDYIKcexOiLyAGU6chc5", null)
            {
                NwkSKey = "3A8DB3AF0B704C8EBA5DDBD41EFB7D30",
            };
            var message = Convert.FromBase64String("QDH1dgOAoRIGdDYIKcexOiLyAGU6chc5");
            var payload = new LoRaPayloadData(message);
            var result = loRaDevice.ValidateMic(payload);
            Assert.True(result);
        }
    }
}
