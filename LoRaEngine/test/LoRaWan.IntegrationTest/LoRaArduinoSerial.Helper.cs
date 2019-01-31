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
        public async Task SetupLora(LoraRegion region)
        {
            try
            {
                if (region == LoraRegion.EU)
                {
                    await this.SetDataRateAsync(LoRaArduinoSerial.Data_Rate_T.DR6, LoRaArduinoSerial.Physical_Type_T.EU868);
                    await this.SetChannelAsync(0, 868.1F);
                    await this.SetChannelAsync(1, 868.3F);
                    await this.SetChannelAsync(2, 868.5F);
                    await this.SetReceiceWindowFirstAsync(0, 868.1F);
                    await this.SetReceiceWindowSecondAsync(868.5F, LoRaArduinoSerial.Data_Rate_T.DR2);
                }
                else
                {
                    await this.SetDataRateAsync(LoRaArduinoSerial.Data_Rate_T.DR0, LoRaArduinoSerial.Physical_Type_T.US915HYBRID);
                }

                await this.SetConfirmedMessageRetryTimeAsync(10);
                await this.SetAdaptiveDataRateAsync(false);
                await this.SetDutyCycleAsync(false);
                await this.SetJoinDutyCycleAsync(false);
                await this.SetPowerAsync(14);
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error during {nameof(this.SetupLora)}. {ex.ToString()}");
            }
        }
    }
}