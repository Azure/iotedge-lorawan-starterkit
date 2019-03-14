// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaTools.Regions;
    using LoRaTools.Utils;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Shared;
    using Microsoft.Extensions.Caching.Memory;
    using Moq;
    using Xunit;

    public class MessageProcessor_End2End_NoDep_CloudToDeviceMessage_SizeLimitBase : MessageProcessorTestBase
    {
        public Rxpk CreateUpstreamRxpk(bool isConfirmed, bool hasMacInUpstream, string datr, SimulatedDevice simulatedDevice)
        {
            Rxpk rxpk = null;
            string msgPayload = null;

            if (isConfirmed)
            {
                if (hasMacInUpstream)
                {
                    // Cofirmed message with Mac command in upstream
                    msgPayload = "02";
                    var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);
                    rxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
                else
                {
                    // Cofirmed message without Mac command in upstream
                    msgPayload = "1234567890";
                    var confirmedMessagePayload = simulatedDevice.CreateConfirmedDataUpMessage(msgPayload);
                    rxpk = confirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
            }
            else
            {
                if (hasMacInUpstream)
                {
                    // Uncofirmed message with Mac command in upstream
                    msgPayload = "02";
                    var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload, isHexPayload: true, fport: 0);
                    rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
                else
                {
                    // Uncofirmed message without Mac command in upstream
                    msgPayload = "1234567890";
                    var unconfirmedMessagePayload = simulatedDevice.CreateUnconfirmedDataUpMessage(msgPayload);
                    rxpk = unconfirmedMessagePayload.SerializeUplink(simulatedDevice.AppSKey, simulatedDevice.NwkSKey, datr: datr).Rxpk[0];
                }
            }

            return rxpk;
        }
    }
}
