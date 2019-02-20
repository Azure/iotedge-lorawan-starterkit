// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWanTest
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using LoRaTools.ADR;
    using LoRaTools.LoRaPhysical;

    class ADRTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            // not enough entries
            var tableentries = new List<LoRaADRTableEntry>();

            for (int i = 0; i < 10; i++)
            {
                tableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = "123",
                    FCnt = i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            Rxpk rxpk = new Rxpk();
            rxpk.Datr = "SF7BW125";

            yield return new object[]
            {
              "123", tableentries, rxpk, null
            };

            // enough entries
            var tableentries2 = new List<LoRaADRTableEntry>();

            for (int i = 0; i < 20; i++)
            {
                tableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = "123",
                    FCnt = i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            Rxpk rxpk2 = new Rxpk();
            rxpk.Datr = "SF7BW125";
            LoRaADRResult loRaADRResult = new LoRaADRResult()
            {
                DataRate = 5,
                NbRepetition = 1,
                TxPower = 0
            };

            yield return new object[]
            {
              "123", tableentries2, rxpk2, loRaADRResult
            };
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
