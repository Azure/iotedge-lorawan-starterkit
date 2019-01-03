using LoRaWan.NetworkServer.V2;
using LoRaWan.Test.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoRaWan.NetworkServer.Test
{
    internal static class TestUtils
    {
        internal static LoRaDevice CreateFromSimulatedDevice(
            SimulatedDevice simulatedDevice,
            ILoRaDeviceClient loRaDeviceClient,
            bool isABP = true)
        {
            var result = new LoRaDevice(simulatedDevice.LoRaDevice.DevAddr, simulatedDevice.LoRaDevice.DeviceID, loRaDeviceClient)
            {
                AppEUI = simulatedDevice.LoRaDevice.AppEUI,
                AppKey = simulatedDevice.LoRaDevice.AppKey,
                SensorDecoder = simulatedDevice.LoRaDevice.SensorDecoder,
                AppSKey = simulatedDevice.LoRaDevice.AppSKey,
                NwkSKey = simulatedDevice.LoRaDevice.NwkSKey,
                GatewayID = simulatedDevice.LoRaDevice.GatewayID
            };
            result.SetFcntDown(simulatedDevice.FrmCntDown);
            result.SetFcntUp(simulatedDevice.FrmCntUp);
            result.IsABP = isABP;

            return result;
        }
    }
}
