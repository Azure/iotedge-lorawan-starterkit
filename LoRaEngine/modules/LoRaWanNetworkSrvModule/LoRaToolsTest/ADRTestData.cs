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
            // First test not enough entries to send back an answer
            var tableentries = new List<LoRaADRTableEntry>();

            var deviceNameNotEnoughEntries = "notenoughentries";

            for (uint i = 0; i < 10; i++)
            {
                tableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = deviceNameNotEnoughEntries,
                    FCnt = i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            Rxpk rxpk = new Rxpk();
            rxpk.Datr = "SF7BW125";
            this.AddRow("Not enough entries to calculate ADR", deviceNameNotEnoughEntries, tableentries, rxpk, true, new LoRaADRResult()
            {
                DataRate = 5,
                TxPower = 0,
                NbRepetition = 1,
                FCntDown = 1
            });

            // **************************************************************
            // Second test enough entries, as very low SNR and max txpower
            // **************************************************************
            var lowerDRTable = new List<LoRaADRTableEntry>();
            var lowerDRDeviceName = "decreaseTxPower";

            for (uint i = 0; i < 21; i++)
            {
                lowerDRTable.Add(new LoRaADRTableEntry()
                {
                    DevEUI = lowerDRDeviceName,
                    FCnt = i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            Rxpk notenoughentriesrxpk = new Rxpk();
            // Set Input DR to 5
            notenoughentriesrxpk.Datr = "SF7BW125";
            LoRaADRResult loRaADRResult = new LoRaADRResult()
            {
                DataRate = 0,
                NbRepetition = 1,
                TxPower = 0
            };
            this.AddRow("ADR setting DR to 0", lowerDRDeviceName, lowerDRTable, notenoughentriesrxpk, false, loRaADRResult);
            // **************************************************************
            // Third test enough entries increase nbrep, as one message every three is received
            // **************************************************************
            var increaseNbReptableentries = new List<LoRaADRTableEntry>();
            var increaseNbRepDeviceName = "Increase NpRep";

            for (uint i = 0; i < 21; i++)
            {
                increaseNbReptableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = increaseNbRepDeviceName,
                    FCnt = 3 * i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            Rxpk increaseNbReprxpk = new Rxpk();
            // DR5
            increaseNbReprxpk.Datr = "SF7BW125";
            LoRaADRResult increaseNbReploRaADRResult = new LoRaADRResult()
            {
                DataRate = 5,
                NbRepetition = 3,
                TxPower = 0,
                FCntDown = 1
            };

            this.AddRow("ADR increase NbRep", increaseNbRepDeviceName, increaseNbReptableentries, increaseNbReprxpk, false, increaseNbReploRaADRResult);

            // **************************************************************
            // Fourth test enough entries decrease nbrep messages pass through
            // ***
            var decreaseNbReptableentries = new List<LoRaADRTableEntry>();
            var decreaseNbRepDeviceName = "Decrease NpRep";

            // start by setting a high number of nbrep
            for (uint i = 0; i < 21; i++)
            {
                decreaseNbReptableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = decreaseNbRepDeviceName,
                    FCnt = 3 * i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = 0
                });
            }

            for (uint i = 61; i < 81; i++)
            {
                decreaseNbReptableentries.Add(
                    new LoRaADRTableEntry()
                    {
                        DevEUI = decreaseNbRepDeviceName,
                        FCnt = i,
                        GatewayCount = 1,
                        GatewayId = "mygateway",
                        Snr = 0
                    });
            }

            Rxpk decreaseNbReprxpk = new Rxpk();
            // DR5
            decreaseNbReprxpk.Datr = "SF7BW125";
            LoRaADRResult decreaseNbReploRaADRResult = new LoRaADRResult()
            {
                DataRate = 5,
                NbRepetition = 1,
                TxPower = 2,
                FCntDown = 1
            };
            this.AddRow("ADR decrease NbRep", decreaseNbRepDeviceName, decreaseNbReptableentries, decreaseNbReprxpk, false, decreaseNbReploRaADRResult);
    }
    }
}
