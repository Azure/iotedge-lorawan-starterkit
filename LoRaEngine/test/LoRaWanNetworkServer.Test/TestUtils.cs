// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.NetworkServer.Test
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using LoRaWan.NetworkServer;
    using LoRaWan.Test.Shared;
    using Microsoft.Azure.Devices.Shared;

    internal static class TestUtils
    {
        internal static LoRaDevice CreateFromSimulatedDevice(
            SimulatedDevice simulatedDevice,
            ILoRaDeviceClient loRaDeviceClient,
            DefaultLoRaDataRequestHandler requestHandler = null)
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

            if (requestHandler != null)
                result.SetRequestHandler(requestHandler);

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

        public static Twin CreateABPTwin(this SimulatedDevice simulatedDevice, Dictionary<string, object> desiredProperties = null)
        {
            var finalDesiredProperties = new Dictionary<string, object>
                {
                    { TwinProperty.DevAddr, simulatedDevice.DevAddr },
                    { TwinProperty.AppSKey, simulatedDevice.AppSKey },
                    { TwinProperty.NwkSKey, simulatedDevice.NwkSKey },
                    { TwinProperty.GatewayID, simulatedDevice.LoRaDevice.GatewayID },
                    { TwinProperty.SensorDecoder, simulatedDevice.LoRaDevice.SensorDecoder },
                };

            if (desiredProperties != null)
            {
                foreach (var kv in desiredProperties)
                {
                    finalDesiredProperties[kv.Key] = kv.Value;
                }
            }

            var reported = new Dictionary<string, object>
            {
                { TwinProperty.FCntDown, simulatedDevice.FrmCntDown },
                { TwinProperty.FCntUp, simulatedDevice.FrmCntUp }
            };

            return CreateTwin(desired: finalDesiredProperties, reported: reported);
        }

        internal static Twin CreateOTAATwin(this SimulatedDevice simulatedDevice, Dictionary<string, object> desiredProperties = null)
        {
            var finalDesiredProperties = new Dictionary<string, object>
                {
                    { TwinProperty.AppEUI, simulatedDevice.AppEUI },
                    { TwinProperty.AppKey, simulatedDevice.AppKey },
                    { TwinProperty.GatewayID, simulatedDevice.LoRaDevice.GatewayID },
                    { TwinProperty.SensorDecoder, simulatedDevice.LoRaDevice.SensorDecoder },
                };

            if (desiredProperties != null)
            {
                foreach (var kv in desiredProperties)
                {
                    finalDesiredProperties[kv.Key] = kv.Value;
                }
            }

            var reported = new Dictionary<string, object>
            {
                { TwinProperty.DevAddr, simulatedDevice.DevAddr },
                { TwinProperty.AppSKey, simulatedDevice.AppSKey },
                { TwinProperty.NwkSKey, simulatedDevice.NwkSKey },
                { TwinProperty.DevNonce, simulatedDevice.DevNonce },
                { TwinProperty.NetID, simulatedDevice.NetId },
                { TwinProperty.FCntDown, simulatedDevice.FrmCntDown },
                { TwinProperty.FCntUp, simulatedDevice.FrmCntUp }
            };

            return CreateTwin(desired: finalDesiredProperties, reported: reported);
        }
    }
}