// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Unit
{
    using System.Collections.Generic;
    using global::LoRaTools.ADR;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.Tests.Common;
    using Xunit;
    using static LoRaWan.DataRateIndex;

#pragma warning disable CA1812 // Unused class
    // Used as Theory Data
    internal class ADRTestData : TheoryData<string, string, List<LoRaADRTableEntry>, RadioMetadata, LoRaADRResult>
#pragma warning restore CA1812 // Unused class
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

            var radioMetadata = new RadioMetadata(DataRateIndex.DR5, TestUtils.TestRegion.GetDefaultRX2ReceiveWindow().Frequency, null);
            AddRow("Not enough entries to calculate ADR", deviceNameNotEnoughEntries, tableentries, radioMetadata, true, new LoRaADRResult()
            {
                DataRate = DR5,
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

            var notenoughentries = new RadioMetadata(DR5, TestUtils.TestRegion.GetDefaultRX2ReceiveWindow().Frequency, null);
            var loRaADRResult = new LoRaADRResult()
            {
                DataRate = 0,
                NbRepetition = 1,
                TxPower = 0
            };
            AddRow("ADR setting DR to 0", lowerDRDeviceName, lowerDRTable, notenoughentries, false, loRaADRResult);
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

            var increaseNbRep = new RadioMetadata(DR5, TestUtils.TestRegion.GetDefaultRX2ReceiveWindow().Frequency, null);
            var increaseNbReploRaADRResult = new LoRaADRResult()
            {
                DataRate = DR5,
                NbRepetition = 3,
                TxPower = 0,
                FCntDown = 1
            };

            AddRow("ADR increase NbRep", increaseNbRepDeviceName, increaseNbReptableentries, increaseNbRep, false, increaseNbReploRaADRResult);

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

            var decreaseNbRep = new RadioMetadata(DR5, TestUtils.TestRegion.GetDefaultRX2ReceiveWindow().Frequency, null);
            var decreaseNbReploRaADRResult = new LoRaADRResult()
            {
                DataRate = DR5,
                NbRepetition = 1,
                TxPower = 2,
                FCntDown = 1
            };
            AddRow("ADR decrease NbRep", decreaseNbRepDeviceName, decreaseNbReptableentries, decreaseNbRep, false, decreaseNbReploRaADRResult);
        }
    }
}
