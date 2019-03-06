// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.IO.Ports;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;

    public sealed partial class LoRaArduinoSerial
    {
        public static LoRaArduinoSerial CreateFromPort(string port)
        {
            var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            LoRaArduinoSerial result = null;

            if (!isWindows)
            {
                TestLogger.Log($"** Starting serial port '{port}' on non-Windows **");

                var serialPort = new SerialDevice(port, BaudRate.B115200);
                result = new LoRaArduinoSerial(serialPort);

                TestLogger.Log($"Opening serial port");
                try
                {
                    serialPort.Open();
                }
                catch (Exception ex)
                {
                    TestLogger.Log($"Error opening serial port '{port}': {ex.ToString()}");
                    throw;
                }
            }
            else
            {
                TestLogger.Log($"** Starting serial port '{port}' on Windows **");

                var serialPortWin = new SerialPort(port)
                {
                    BaudRate = 115200,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    DataBits = 8,
                    DtrEnable = true,
                    Handshake = Handshake.None
                };
                result = new LoRaArduinoSerial(serialPortWin);

                try
                {
                    serialPortWin.Open();
                }
                catch (Exception ex)
                {
                    TestLogger.Log($"Error opening serial port '{port}': {ex.ToString()}");
                    throw;
                }
            }

            return result;
        }

        // Setup lora for a given region
        public async Task SetupLora(
            LoraRegion region,
            LoRaArduinoSerial._data_rate_t dataRate = LoRaArduinoSerial._data_rate_t.DR6,
            short power = 14,
            bool adr = false)
        {
            try
            {
                await this.setAdaptiveDataRateAsync(adr);

                if (region == LoraRegion.EU)
                {
                    await this.setDataRateAsync(dataRate, LoRaArduinoSerial._physical_type_t.EU868);
                    await this.setChannelAsync(0, 868.1F);
                    await this.setChannelAsync(1, 868.3F);
                    await this.setChannelAsync(2, 868.5F);
                    await this.setChannelAsync(3, 867.1F);
                    await this.setChannelAsync(4, 867.3F);
                    await this.setChannelAsync(5, 867.5F);
                    await this.setChannelAsync(6, 867.7F);
                    await this.setReceiceWindowFirstAsync(0, 868.1F);
                    await this.setReceiceWindowSecondAsync(869.5F, LoRaArduinoSerial._data_rate_t.DR0);
                }
                else
                {
                    await this.setDataRateAsync(LoRaArduinoSerial._data_rate_t.DR0, LoRaArduinoSerial._physical_type_t.US915HYBRID);
                }

                await this.setConfirmedMessageRetryTimeAsync(10);
                await this.setDutyCycleAsync(false);
                await this.setJoinDutyCycleAsync(false);
                await this.setPowerAsync(power);
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error during {nameof(this.SetupLora)}. {ex.ToString()}");
            }
        }
    }
}