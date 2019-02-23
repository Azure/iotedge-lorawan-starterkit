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
    using Xunit;

    class ADRTestData : TheoryData<string, string, List<LoRaADRTableEntry>, Rxpk, LoRaADRResult>
    {
        public ADRTestData()
        {
            // First test not enough entries
            var tableentries = new List<LoRaADRTableEntry>();

            for (int i = 0; i < 10; i++)
            {
                tableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = "notenoughentries",
                    FCnt = i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            Rxpk rxpk = new Rxpk();
            rxpk.Datr = "SF7BW125";
            this.AddRow("Not enough entries to calculate ADR", "notenoughentries", tableentries, rxpk, null);

            // Second test enough entries
            var notenoughentriestableentries = new List<LoRaADRTableEntry>();

            for (int i = 0; i < 20; i++)
            {
                notenoughentriestableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = "decreaseTxPower",
                    FCnt = i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            Rxpk notenoughentriesrxpk = new Rxpk();
            notenoughentriesrxpk.Datr = "SF7BW125";
            LoRaADRResult loRaADRResult = new LoRaADRResult()
            {
                DataRate = 0,
                NbRepetition = 1,
                TxPower = 7
            };

            this.AddRow("ADR decreasing Tx Power", "decreaseTxPower", notenoughentriestableentries, notenoughentriesrxpk, loRaADRResult);

            // Third test enough entries increase nbrep
            var increaseNbReptableentries = new List<LoRaADRTableEntry>();

            for (int i = 0; i < 20; i++)
            {
                increaseNbReptableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = "decreaseNbRep",
                    FCnt = 3 * i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            Rxpk increaseNbReprxpk = new Rxpk();
            increaseNbReprxpk.Datr = "SF7BW125";
            LoRaADRResult increaseNbReploRaADRResult = new LoRaADRResult()
            {
                DataRate = 5,
                NbRepetition = 3,
                TxPower = 7
            };

            this.AddRow("ADR decrease NbRep", "decreaseNbRep", increaseNbReptableentries, increaseNbReprxpk, increaseNbReploRaADRResult);
        }
    }
}
