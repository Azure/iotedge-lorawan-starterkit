// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.Integration
{
    using LoRaTools.LoRaMessage;
    using LoRaWan.NetworkServer.BasicsStation;
    using LoRaWan.Tests.Common;

    public class CloudToDeviceMessageSizeLimitBaseTests : MessageProcessorTestBase
    {
        public static (RadioMetadata RadioMetadata, LoRaPayload LoRaPayload) CreateUpstreamMessage(bool isConfirmed, bool hasMacInUpstream, DataRate datr, SimulatedDevice simulatedDevice)
        {
            LoRaPayload loRaPayload;
            string msgPayload;

            var datarateindex = TestUtils.TestRegion.GetDRFromFreqAndChan(datr);
            var radioMetadata = TestUtils.GenerateTestRadioMetadata(dataRate: datarateindex);

            if (isConfirmed)
            {
                if (hasMacInUpstream)
                {
                    // Cofirmed message with Mac command in upstream
                    msgPayload = "02";
                    loRaPayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);
                }
                else
                {
                    // Cofirmed message without Mac command in upstream
                    msgPayload = "1234567890";
                    loRaPayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload);
                }
            }
            else
            {
                if (hasMacInUpstream)
                {
                    // Uncofirmed message with Mac command in upstream
                    msgPayload = "02";
                    loRaPayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);
                }
                else
                {
                    // Uncofirmed message without Mac command in upstream
                    msgPayload = "1234567890";
                    loRaPayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload);
                }
            }

            return (radioMetadata, loRaPayload);
        }
    }
}
