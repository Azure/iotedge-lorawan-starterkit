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
    internal class ADRTestData : TheoryData<string, DevEui, List<LoRaADRTableEntry>, RadioMetadata, LoRaADRResult>
#pragma warning restore CA1812 // Unused class
    {
        public ADRTestData()
        {
            // First test not enough entries to send back an answer
            var tableentries = new List<LoRaADRTableEntry>();

            var deviceEuiNotEnoughEntries = TestEui.GenerateDevEui();

            for (uint i = 0; i < 10; i++)
            {
                tableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = deviceEuiNotEnoughEntries,
                    FCnt = i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            var radioMetadata = new RadioMetadata(DataRateIndex.DR5, TestUtils.TestRegion.GetDefaultRX2ReceiveWindow(default).Frequency, null);
            AddRow("Not enough entries to calculate ADR", deviceEuiNotEnoughEntries, tableentries, radioMetadata, true, new LoRaADRResult()
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
            var lowerDRDeviceEui = TestEui.GenerateDevEui();

            for (uint i = 0; i < 21; i++)
            {
                lowerDRTable.Add(new LoRaADRTableEntry()
                {
                    DevEUI = lowerDRDeviceEui,
                    FCnt = i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            var notenoughentries = new RadioMetadata(DR5, TestUtils.TestRegion.GetDefaultRX2ReceiveWindow(default).Frequency, null);
            var loRaADRResult = new LoRaADRResult()
            {
                DataRate = 0,
                NbRepetition = 1,
                TxPower = 0
            };
            AddRow("ADR setting DR to 0", lowerDRDeviceEui, lowerDRTable, notenoughentries, false, loRaADRResult);
            // **************************************************************
            // Third test enough entries increase nbrep, as one message every three is received
            // **************************************************************
            var increaseNbReptableentries = new List<LoRaADRTableEntry>();
            var increaseNbRepDeviceEui = TestEui.GenerateDevEui();

            for (uint i = 0; i < 21; i++)
            {
                increaseNbReptableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = increaseNbRepDeviceEui,
                    FCnt = 3 * i,
                    GatewayCount = 1,
                    GatewayId = "mygateway",
                    Snr = -20
                });
            }

            var increaseNbRep = new RadioMetadata(DR5, TestUtils.TestRegion.GetDefaultRX2ReceiveWindow(default).Frequency, null);
            var increaseNbReploRaADRResult = new LoRaADRResult()
            {
                DataRate = DR5,
                NbRepetition = 3,
                TxPower = 0,
                FCntDown = 1
            };

            AddRow("ADR increase NbRep", increaseNbRepDeviceEui, increaseNbReptableentries, increaseNbRep, false, increaseNbReploRaADRResult);

            // **************************************************************
            // Fourth test enough entries decrease nbrep messages pass through
            // ***
            var decreaseNbReptableentries = new List<LoRaADRTableEntry>();
            var decreaseNbRepDeviceEui = TestEui.GenerateDevEui();

            // start by setting a high number of nbrep
            for (uint i = 0; i < 21; i++)
            {
                decreaseNbReptableentries.Add(new LoRaADRTableEntry()
                {
                    DevEUI = decreaseNbRepDeviceEui,
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
                        DevEUI = decreaseNbRepDeviceEui,
                        FCnt = i,
                        GatewayCount = 1,
                        GatewayId = "mygateway",
                        Snr = 0
                    });
            }

            var decreaseNbRep = new RadioMetadata(DR5, TestUtils.TestRegion.GetDefaultRX2ReceiveWindow(default).Frequency, null);
            var decreaseNbReploRaADRResult = new LoRaADRResult()
            {
                DataRate = DR5,
                NbRepetition = 1,
                TxPower = 2,
                FCntDown = 1
            };
            AddRow("ADR decrease NbRep", decreaseNbRepDeviceEui, decreaseNbReptableentries, decreaseNbRep, false, decreaseNbReploRaADRResult);
        }
    }
}
