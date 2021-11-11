// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.IO.Ports;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;

    /// <summary>
    /// Lora Arduino Serial Class.
    /// </summary>
    public sealed partial class LoRaArduinoSerial
    {
        public static LoRaArduinoSerial CreateFromPort(string port)
        {
            TestLogger.Log($"** Starting serial port '{port}' **");

            var serialPort = new SerialPort(port)
            {
                BaudRate = 115200,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DataBits = 8,
                DtrEnable = true,
                Handshake = Handshake.None
            };
            var result = new LoRaArduinoSerial(serialPort);

            try
            {
                serialPort.Open();
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error opening serial port '{port}': {ex}");
                throw;
            }

            return result;
        }

        public Task SetupLora(TestConfiguration configuration) =>
            SetupLora(configuration.LoraRegion, power: configuration.TxPower);


        // Setup lora for a given region
        public async Task SetupLora(
            LoraRegion region,
            _data_rate_t dataRate = _data_rate_t.DR6,
            short power = 14,
            bool adr = false)
        {
            try
            {
                await setAdaptiveDataRateAsync(adr);

                if (region == LoraRegion.EU)
                {
                    await setDataRateAsync(dataRate, _physical_type_t.EU868);
                    await setChannelAsync(0, 868.1F);
                    await setChannelAsync(1, 868.3F);
                    await setChannelAsync(2, 868.5F);
                    await setChannelAsync(3, 867.1F);
                    await setChannelAsync(4, 867.3F);
                    await setChannelAsync(5, 867.5F);
                    await setChannelAsync(6, 867.7F);
                    await setReceiceWindowFirstAsync(0, 868.1F);
                    await setReceiceWindowSecondAsync(869.5F, _data_rate_t.DR0);
                }
                else
                {
                    await setDataRateAsync(_data_rate_t.DR0, _physical_type_t.US915HYBRID);
                }

                await setConfirmedMessageRetryTimeAsync(10);
                await setDutyCycleAsync(false);
                await setJoinDutyCycleAsync(false);
                await setPowerAsync(power);
            }
#pragma warning disable CA1031 // Do not catch general exception types.
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error during {nameof(SetupLora)}. {ex}");
            }
        }
    }
}
