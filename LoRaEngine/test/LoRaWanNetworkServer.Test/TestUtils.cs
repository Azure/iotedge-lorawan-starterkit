//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
using LoRaWan.NetworkServer;
using LoRaWan.Test.Shared;
using Microsoft.Azure.Devices.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer.Test
{
    internal static class TestUtils
    {
        internal static LoRaDevice CreateFromSimulatedDevice(
            SimulatedDevice simulatedDevice,
            ILoRaDeviceClient loRaDeviceClient)
        {
            var result = new LoRaDevice(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, loRaDeviceClient)
            {
                AppEUI = simulatedDevice.LoRaDevice.AppEUI,
                AppKey = simulatedDevice.LoRaDevice.AppKey,
                SensorDecoder = simulatedDevice.LoRaDevice.SensorDecoder,
                AppSKey = simulatedDevice.LoRaDevice.AppSKey,
                NwkSKey = simulatedDevice.LoRaDevice.NwkSKey,
                GatewayID = simulatedDevice.LoRaDevice.GatewayID,
                IsOurDevice = true,
            };
            result.SetFcntDown(simulatedDevice.FrmCntDown);
            result.SetFcntUp(simulatedDevice.FrmCntUp);

            return result;
        }

        internal static Twin CreateTwin(Dictionary<string, object> desired = null, Dictionary<string, object> reported = null)
        {
            var twin = new Twin();
            if (desired != null)
            {
                foreach (var kv in desired)
                {
                    twin.Properties.Desired[kv.Key] = kv.Value;
                }
            }

            if (reported != null)
            {
                foreach (var kv in reported)
                {
                    twin.Properties.Reported[kv.Key] = kv.Value;
                }
            }

            return twin;
        }

        public static Twin CreateABPTwin(this SimulatedDevice simulatedDevice)
        {
            return CreateTwin(
                desired: new Dictionary<string, object>
                {
                    { TwinProperty.DevAddr, simulatedDevice.DevAddr },
                    { TwinProperty.AppSKey, simulatedDevice.AppSKey },
                    { TwinProperty.NwkSKey, simulatedDevice.NwkSKey },
                    { TwinProperty.GatewayID, simulatedDevice.LoRaDevice.GatewayID },
                    { TwinProperty.SensorDecoder, simulatedDevice.LoRaDevice.SensorDecoder },
                }
            );
        }
    }
}
