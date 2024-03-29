// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*
  LoRaWAN.h
  2013 Copyright (c) Seeed Technology Inc.  All right reserved.

  Author: Wayne Weng
  Date: 2016-10-17

  add rgb backlight fucnction @ 2013-10-15

  The MIT License (MIT)

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in
  all copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
  THE SOFTWARE.1  USA
*/

#pragma warning disable CA1008 // Enums should have zero value
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CA1822 // Mark members as static


namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO.Ports;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.Tests.Common;

    /// <summary>
    /// Arduino driver for the LoRaWAN device.
    /// </summary>
    public sealed partial class LoRaArduinoSerial : IDisposable
    {
        private const int DEFAULT_TIMEWAIT = 100;
        private SerialPort serialPort;

        public enum _class_type_t
        {
            CLASS_A = 0,
            CLASS_C
        }

        public enum _physical_type_t
        {
            EU434 = 0,
            EU868,
            US915,
            US915HYBRID,
            AU915,
            AU915OLD,
            CN470,
            CN779,
            AS923,
            KR920,
            IN865
        }

        public enum _device_mode_t
        {
            LWABP = 0,
            LWOTAA,
            TEST
        }

        public enum _otaa_join_cmd_t
        {
            JOIN = 0,
            FORCE
        }

        public enum _window_delay_t
        {
            RECEIVE_DELAY1 = 0,
            RECEIVE_DELAY2,
            JOIN_ACCEPT_DELAY1,
            JOIN_ACCEPT_DELAY2
        }

        public enum _band_width_t
        {
            BW125 = 125,
            BW250 = 250,
            BW500 = 500
        }

        public enum _spreading_factor_t
        {
            SF12 = 12,
            SF11 = 11,
            SF10 = 10,
            SF9 = 9,
            SF8 = 8,
            SF7 = 7
        }

        public enum _data_rate_t
        {
            DR0 = 0,
            DR1,
            DR2,
            DR3,
            DR4,
            DR5,
            DR6,
            DR7,
            DR8,
            DR9,
            DR10,
            DR11,
            DR12,
            DR13,
            DR14,
            DR15
        }

        private readonly byte[] serialPortBuffer;

        private LoRaArduinoSerial(SerialPort sp)
        {
            this.serialPort = sp;
            this.serialPortBuffer = new byte[this.serialPort.ReadBufferSize];
            this.serialPort.DataReceived += OnSerialDeviceData;
        }

        private void OnSerialDeviceData(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var myserialPort = (SerialPort)sender;
                var input = myserialPort.ReadLine();
                // var readCount = myserialPort.Read(this.serialPortBuffer, 0, this.serialPortBuffer.Length);
                // var dataread = System.Text.Encoding.UTF8.GetString(this.serialPortBuffer, 0, readCount);
                OnSerialDataReceived(input);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error in serial data rx. {ex}");
            }
        }

        private void OnSerialDataReceived(string rawData)
        {
            try
            {
                var data = rawData.Replace("\r", string.Empty, StringComparison.Ordinal);

                var lines = string.Concat(this.buff, data).Split('\n');
                this.buff = string.Empty;

                if (lines.Length > 0)
                {
                    for (var i = 0; i < lines.Length - 1; i++)
                    {
                        if (!string.IsNullOrEmpty(lines[i]))
                        {
                            AppendSerialLog(lines[i]);
                        }
                    }

                    // last line: does the input ends with a new line?
                    var lastParsedLine = lines[^1];
                    if (!string.IsNullOrEmpty(lastParsedLine))
                    {
                        // add as finished line
                        AppendSerialLog(lastParsedLine);
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error processing serial data. {ex}");
            }
        }

        private readonly ConcurrentQueue<string> serialLogs = new ConcurrentQueue<string>();
        private string buff = string.Empty;

        private void AppendSerialLog(string message)
        {
            TestLogger.Log($"[SERIAL] {message}");
            this.serialLogs.Enqueue(message);
        }

        public IReadOnlyCollection<string> SerialLogs => this.serialLogs;

        // Gets/sets if serial writes should be logged
        // Disabled by default
        public bool LogWrites { get; set; }

        public void ClearSerialLogs()
        {
            TestLogger.Log($"*** Clearing serial logs ({this.serialLogs.Count}) ***");
            this.serialLogs.Clear();
        }

        public LoRaArduinoSerial setId(string DevAddr, string DevEUI, string AppEUI)
        {
            if (!string.IsNullOrEmpty(DevAddr))
            {
                var cmd = $"AT+ID=DevAddr,{DevAddr}\r\n";
                this.sendCommand(cmd);

                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            if (!string.IsNullOrEmpty(DevEUI))
            {
                var cmd = $"AT+ID=DevEui,{DevEUI}\r\n";
                this.sendCommand(cmd);
                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            if (!string.IsNullOrEmpty(AppEUI))
            {
                var cmd = $"AT+ID=AppEui,{AppEUI}\r\n";
                this.sendCommand(cmd);
                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            return this;
        }

        public Task setIdAsync(DevAddr? DevAddr, string DevEUI, JoinEui? AppEUI) =>
            setIdAsync(DevAddr?.ToString(), DevEUI, AppEUI?.ToString());

        private async Task setIdAsync(string DevAddr, string DevEUI, string AppEUI)
        {
            try
            {
                if (!string.IsNullOrEmpty(DevAddr))
                {
                    var cmd = $"AT+ID=DevAddr,{DevAddr}\r\n";
                    this.sendCommand(cmd);

                    await EnsureSerialAnswerAsync("+ID: DevAddr", 30);
                }

                if (!string.IsNullOrEmpty(DevEUI))
                {
                    var cmd = $"AT+ID=DevEui,{DevEUI}\r\n";
                    this.sendCommand(cmd);

                    await EnsureSerialAnswerAsync("+ID: DevEui", 30);
                }

                if (!string.IsNullOrEmpty(AppEUI))
                {
                    var cmd = $"AT+ID=AppEui,{AppEUI}\r\n";
                    this.sendCommand(cmd);

                    await EnsureSerialAnswerAsync("+ID: AppEui", 30);
                }
            }
            catch (ArduinoDeviceFailedException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error during {nameof(this.setIdAsync)}. {ex}");
            }
        }

        public LoRaArduinoSerial setKey(string NwkSKey, string AppSKey, string AppKey)
        {
            if (!string.IsNullOrEmpty(NwkSKey))
            {
                var cmd = $"AT+KEY=NWKSKEY,{NwkSKey}\r\n";
                this.sendCommand(cmd);
                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            if (!string.IsNullOrEmpty(AppSKey))
            {
                var cmd = $"AT+KEY=APPSKEY,{AppSKey}\r\n";
                this.sendCommand(cmd);

                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            if (!string.IsNullOrEmpty(AppKey))
            {
                var cmd = $"AT+KEY= APPKEY,{AppKey}\r\n";
                this.sendCommand(cmd);

                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            return this;
        }

        public async Task setKeyAsync(NetworkSessionKey? NwkSKey, AppSessionKey? AppSKey, AppKey? AppKey)
        {
            try
            {
                if (NwkSKey is { } someNetworkSessionKey)
                {
                    var cmd = $"AT+KEY=NWKSKEY,{someNetworkSessionKey}\r\n";
                    this.sendCommand(cmd);
                    await EnsureSerialAnswerAsync("+KEY: NWKSKEY", 30);
                }

                if (AppSKey is { } someAppSessionKey)
                {
                    var cmd = $"AT+KEY=APPSKEY,{someAppSessionKey}\r\n";
                    this.sendCommand(cmd);

                    await EnsureSerialAnswerAsync("+KEY: APPSKEY", 30);
                }

                if (AppKey is { } someAppKey)
                {
                    var cmd = $"AT+KEY= APPKEY,{someAppKey}\r\n";
                    this.sendCommand(cmd);

                    await EnsureSerialAnswerAsync("+KEY: APPKEY", 30);
                }
            }
            catch (ArduinoDeviceFailedException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error during {nameof(this.setKeyAsync)}. {ex}");
            }
        }

        public LoRaArduinoSerial setDataRate(_data_rate_t dataRate, _physical_type_t physicalType)
        {
            if (physicalType == _physical_type_t.EU434)
                this.sendCommand("AT+DR=EU433\r\n");
            else if (physicalType == _physical_type_t.EU868)
                this.sendCommand("AT+DR=EU868\r\n");
            else if (physicalType == _physical_type_t.US915)
                this.sendCommand("AT+DR=US915\r\n");
            else if (physicalType == _physical_type_t.US915HYBRID)
                this.sendCommand("AT+DR=US915HYBRID\r\n");
            else if (physicalType == _physical_type_t.AU915)
                this.sendCommand("AT+DR=AU915\r\n");
            else if (physicalType == _physical_type_t.AU915OLD)
                this.sendCommand("AT+DR=AU915OLD\r\n");
            else if (physicalType == _physical_type_t.CN470)
                this.sendCommand("AT+DR=CN470\r\n");
            else if (physicalType == _physical_type_t.CN779)
                this.sendCommand("AT+DR=CN779\r\n");
            else if (physicalType == _physical_type_t.AS923)
                this.sendCommand("AT+DR=AS923\r\n");
            else if (physicalType == _physical_type_t.KR920)
                this.sendCommand("AT+DR=KR920\r\n");
            else if (physicalType == _physical_type_t.IN865)
                this.sendCommand("AT+DR=IN865\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            var cmd = $"AT+DR={dataRate}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setDataRateAsync(_data_rate_t dataRate, _physical_type_t physicalType)
        {
            if (physicalType == _physical_type_t.EU434)
                this.sendCommand("AT+DR=EU433\r\n");
            else if (physicalType == _physical_type_t.EU868)
                this.sendCommand("AT+DR=EU868\r\n");
            else if (physicalType == _physical_type_t.US915)
                this.sendCommand("AT+DR=US915\r\n");
            else if (physicalType == _physical_type_t.US915HYBRID)
                this.sendCommand("AT+DR=US915HYBRID\r\n");
            else if (physicalType == _physical_type_t.AU915)
                this.sendCommand("AT+DR=AU915\r\n");
            else if (physicalType == _physical_type_t.AU915OLD)
                this.sendCommand("AT+DR=AU915OLD\r\n");
            else if (physicalType == _physical_type_t.CN470)
                this.sendCommand("AT+DR=CN470\r\n");
            else if (physicalType == _physical_type_t.CN779)
                this.sendCommand("AT+DR=CN779\r\n");
            else if (physicalType == _physical_type_t.AS923)
                this.sendCommand("AT+DR=AS923\r\n");
            else if (physicalType == _physical_type_t.KR920)
                this.sendCommand("AT+DR=KR920\r\n");
            else if (physicalType == _physical_type_t.IN865)
                this.sendCommand("AT+DR=IN865\r\n");

            await EnsureSerialAnswerAsync("+DR:", 30);

            await Task.Delay(DEFAULT_TIMEWAIT);

            var cmd = $"AT+DR={dataRate}\r\n";
            this.sendCommand(cmd);

            await EnsureSerialAnswerAsync("+DR:", 30);
        }

        public LoRaArduinoSerial setPower(short power)
        {
            var cmd = $"AT+POWER={power}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setPowerAsync(short power)
        {
            var cmd = $"AT+POWER={power}\r\n";
            this.sendCommand(cmd);

            await EnsureSerialAnswerAsync("+POWER:", 30);
        }

        public async Task setPortAsync(int port)
        {
            var cmd = $"AT+PORT={port}\r\n";
            this.sendCommand(cmd);

            await EnsureSerialAnswerAsync("+PORT:", 30);
        }

        public LoRaArduinoSerial setAdaptiveDataRate(bool command)
        {
            if (command)
                this.sendCommand("AT+ADR=ON\r\n");
            else
                this.sendCommand("AT+ADR=OFF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setAdaptiveDataRateAsync(bool command)
        {
            if (command)
                this.sendCommand("AT+ADR=ON\r\n");
            else
                this.sendCommand("AT+ADR=OFF\r\n");

            await EnsureSerialAnswerAsync("+ADR:", 30);
        }

        // Wait until the serial data is empty
        internal async Task<bool> WaitForIdleAsync(TimeSpan? timeout = null)
        {
            var timeoutToUse = timeout ?? TimeSpan.FromSeconds(60);
            var delayTime = timeoutToUse / 2;
            var start = DateTime.UtcNow;

            do
            {
                ClearSerialLogs();
                await Task.Delay(delayTime);

                if (!SerialLogs.Any())
                    return true;
            }
            while (start.Subtract(DateTime.UtcNow) <= timeoutToUse);

            return false;
        }

        public LoRaArduinoSerial setChannel(int channel, float frequency)
        {
            var cmd = $"AT+CH={channel},{(short)frequency}.{(short)(frequency * 10) % 10}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setChannelAsync(int channel, float frequency)
        {
            var cmd = $"AT+CH={channel},{(short)frequency}.{(short)(frequency * 10) % 10}\r\n";
            this.sendCommand(cmd);

            await EnsureSerialAnswerAsync("+CH:", 30);
        }

        public LoRaArduinoSerial setChannel(char channel, float frequency, _data_rate_t dataRata)
        {
            var cmd = $"AT+CH={channel},{(short)frequency}.{(short)(frequency * 10) % 10},{dataRata}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial setChannel(char channel, float frequency, _data_rate_t dataRataMin, _data_rate_t dataRataMax)
        {
            var cmd = $"AT+CH={channel},{(short)frequency}.{(short)(frequency * 10) % 10},{dataRataMin},{dataRataMax}\r\n";

            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task<bool> transferPacketAsync(string buffer, int timeout)
        {
            try
            {
                this.sendCommand("AT+MSG=\"");

                this.sendCommand(buffer);

                this.sendCommand("\"\r\n");

                var start = DateTime.UtcNow;

                while (true)
                {
                    if (ReceivedSerial(x => x.StartsWith("+MSG: Done", StringComparison.Ordinal)))
                        return true;
                    else if (start.AddSeconds(timeout) < DateTime.UtcNow)
                        return false;

                    await Task.Delay(100);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error during {nameof(this.transferPacketAsync)}. {ex}");
                return false;
            }
        }

        public async Task<bool> transferHexPacketAsync(string buffer, int timeout)
        {
            try
            {
                this.sendCommand("AT+MSGHEX=\"");

                this.sendCommand(buffer);

                this.sendCommand("\"\r\n");

                var start = DateTime.UtcNow;

                while (true)
                {
                    if (ReceivedSerial(x => x.StartsWith("+MSGHEX: Done", StringComparison.Ordinal)))
                        return true;
                    else if (start.AddSeconds(timeout) < DateTime.UtcNow)
                        return false;

                    await Task.Delay(100);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error during {nameof(this.transferHexPacketAsync)}. {ex}");
                return false;
            }
        }

        public bool transferPacket(string buffer, int timeout)
        {
            this.sendCommand("AT+MSG=\"");

            this.sendCommand(buffer);

            this.sendCommand("\"\r\n");

            var start = DateTime.UtcNow;

            while (true)
            {
                if (ReceivedSerial(x => x.StartsWith("+MSG: Done", StringComparison.Ordinal)))
                    return true;
                else if (start.AddSeconds(timeout) < DateTime.UtcNow)
                    return false;
            }
        }

        private bool ReceivedSerial(Func<string, bool> predicate)
        {
            foreach (var serialLine in SerialLogs)
            {
                if (predicate(serialLine))
                    return true;
            }

            return false;
        }

        public bool transferPacketWithConfirmed(string buffer, int timeout)
        {
            this.sendCommand("AT+CMSG=\"");
            this.sendCommand(buffer);
            this.sendCommand("\"\r\n");

            var start = DateTime.UtcNow;

            while (true)
            {
                if (ReceivedSerial(x => x.StartsWith("+CMSG: ACK Received", StringComparison.Ordinal)))
                    return true;
                else if (start.AddSeconds(timeout) < DateTime.UtcNow)
                    return false;
            }

            // if (_buffer.Contains("+CMSG: ACK Received")) return true;
        }

        public async Task<bool> transferPacketWithConfirmedAsync(string buffer, int timeout)
        {
            try
            {
                this.sendCommand("AT+CMSG=\"");
                this.sendCommand(buffer);
                this.sendCommand("\"\r\n");

                var start = DateTime.UtcNow;

                while (true)
                {
                    if (ReceivedSerial(x => x.StartsWith("+CMSG: ACK Received", StringComparison.Ordinal)))
                        return true;
                    else if (start.AddSeconds(timeout) < DateTime.UtcNow)
                        return false;

                    await Task.Delay(100);
                }

                // if (_buffer.Contains("+CMSG: ACK Received")) return true;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error during {nameof(this.transferPacketWithConfirmedAsync)}. {ex}");
                return false;
            }
        }

        public LoRaArduinoSerial setUnconfirmedMessageRepeatTime(uint time)
        {
            if (time > 15)
                time = 15;
            else if (time == 0)
                time = 1;

            var cmd = $"AT+REPT={time}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial setConfirmedMessageRetryTime(uint time)
        {
            if (time > 15)
                time = 15;
            else if (time == 0)
                time = 1;

            var cmd = $"AT+RETRY={time}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setConfirmedMessageRetryTimeAsync(uint time)
        {
            if (time > 15)
                time = 15;
            else if (time == 0)
                time = 1;

            var cmd = $"AT+RETRY={time}\r\n";
            this.sendCommand(cmd);

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial setReceiceWindowFirst(bool command)
        {
            if (command)
                this.sendCommand("AT+RXWIN1=ON\r\n");
            else
                this.sendCommand("AT+RXWIN1=OFF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial setReceiceWindowFirst(int channel, float frequency)
        {
            var cmd = $"AT+RXWIN1={channel},{(short)frequency}.{(short)(frequency * 10) % 10}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setReceiceWindowFirstAsync(int channel, float frequency)
        {
            var cmd = $"AT+RXWIN1={channel},{(short)frequency}.{(short)(frequency * 10) % 10}\r\n";
            this.sendCommand(cmd);

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial setReceiceWindowSecond(float frequency, _data_rate_t dataRate)
        {
            var cmd = $"AT+RXWIN2={(short)frequency}.{(short)(frequency * 10) % 10},{dataRate}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setReceiceWindowSecondAsync(float frequency, _data_rate_t dataRate)
        {
            var cmd = $"AT+RXWIN2={(short)frequency}.{(short)(frequency * 10) % 10},{dataRate}\r\n";
            this.sendCommand(cmd);

            await EnsureSerialAnswerAsync("+RXWIN2:", 10);
        }

        public LoRaArduinoSerial setReceiceWindowSecond(float frequency, _spreading_factor_t spreadingFactor, _band_width_t bandwidth)
        {
            var cmd = $"AT+RXWIN2={(short)frequency}.{(short)(frequency * 10) % 10},{spreadingFactor},{bandwidth}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial setDutyCycle(bool command)
        {
            if (command)
                this.sendCommand("AT+LW=DC, ON\r\n");
            else
                this.sendCommand("AT+LW=DC, OFF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setDutyCycleAsync(bool command)
        {
            if (command)
                this.sendCommand("AT+LW=DC, ON\r\n");
            else
                this.sendCommand("AT+LW=DC, OFF\r\n");

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial setJoinDutyCycle(bool command)
        {
            if (command)
                this.sendCommand("AT+LW=JDC,ON\r\n");
            else
                this.sendCommand("AT+LW=JDC,OFF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);
            return this;
        }

        public async Task setJoinDutyCycleAsync(bool command)
        {
            if (command)
                this.sendCommand("AT+LW=JDC,ON\r\n");
            else
                this.sendCommand("AT+LW=JDC,OFF\r\n");

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial setReceiceWindowDelay(_window_delay_t command, short _delay)
        {
            var cmd = string.Empty;

            if (command == _window_delay_t.RECEIVE_DELAY1)
                cmd = $"AT+DELAY=RX1,{_delay}\r\n";
            else if (command == _window_delay_t.RECEIVE_DELAY2)
                cmd = $"AT+DELAY=RX2,{_delay}\r\n";
            else if (command == _window_delay_t.JOIN_ACCEPT_DELAY1)
                cmd = $"AT+DELAY=JRX1,{_delay}\r\n";
            else if (command == _window_delay_t.JOIN_ACCEPT_DELAY2)
                cmd = $"AT+DELAY=JRX2,{_delay}\r\n";
            this.sendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);
            return this;
        }

        public LoRaArduinoSerial setClassType(_class_type_t type)
        {
            if (type == _class_type_t.CLASS_A)
                this.sendCommand("AT+CLASS=A\r\n");
            else if (type == _class_type_t.CLASS_C)
                this.sendCommand("AT+CLASS=C\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setClassTypeAsync(_class_type_t type)
        {
            if (type == _class_type_t.CLASS_A)
                this.sendCommand("AT+CLASS=A\r\n");
            else if (type == _class_type_t.CLASS_C)
                this.sendCommand("AT+CLASS=C\r\n");

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial setDeciveMode(_device_mode_t mode)
        {
            if (mode == _device_mode_t.LWABP)
                this.sendCommand("AT+MODE=LWABP\r\n");
            else if (mode == _device_mode_t.LWOTAA)
                this.sendCommand("AT+MODE=LWOTAA\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setDeviceModeAsync(_device_mode_t mode)
        {
            try
            {
                if (mode == _device_mode_t.LWABP)
                    this.sendCommand("AT+MODE=LWABP\r\n");
                else if (mode == _device_mode_t.LWOTAA)
                    this.sendCommand("AT+MODE=LWOTAA\r\n");
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error during {nameof(this.setDeviceModeAsync)}. {ex}");
            }

            await EnsureSerialAnswerAsync("+MODE:", 30);
        }

        public bool setOTAAJoin(_otaa_join_cmd_t command, int timeout)
        {
            if (command == _otaa_join_cmd_t.JOIN)
                this.sendCommand("AT+JOIN\r\n");
            else if (command == _otaa_join_cmd_t.FORCE)
                this.sendCommand("AT+JOIN=FORCE\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            var start = DateTime.UtcNow;
            while (true)
            {
                if (ReceivedSerial(x => x.StartsWith("+JOIN: Done", StringComparison.Ordinal)))
                    return true;
                else if (ReceivedSerial(x => x.StartsWith("+JOIN: LoRaWAN modem is busy", StringComparison.Ordinal)))
                    return false;
                else if (ReceivedSerial(x => x.StartsWith("+JOIN: Join failed", StringComparison.Ordinal)))
                    return false;
                else if (start.AddMilliseconds(timeout) < DateTime.UtcNow)
                    return false;
            }

            // ptr = _buffer.Contains("+JOIN: Join failed");
            // if (ptr) return false;
            // ptr = _buffer.Contains("+JOIN: LoRaWAN modem is busy");
            // if (ptr) return false;
        }

        public async Task<bool> setOTAAJoinAsyncWithRetry(_otaa_join_cmd_t command, int timeoutPerTry, int retries)
        {
            for (var attempt = 1; attempt <= retries; ++attempt)
            {
                TestLogger.Log($"Join attempt #{attempt}/{retries}");
                if (command == _otaa_join_cmd_t.JOIN)
                    this.sendCommand("AT+JOIN\r\n");
                else if (command == _otaa_join_cmd_t.FORCE)
                    this.sendCommand("AT+JOIN=FORCE\r\n");

                await Task.Delay(DEFAULT_TIMEWAIT);

                var start = DateTime.UtcNow;

                while (DateTime.UtcNow.Subtract(start).TotalMilliseconds < timeoutPerTry)
                {
                    if (ReceivedSerial((s) => s.Contains("+JOIN: Network joined", StringComparison.Ordinal)))
                        return true;
                    else if (ReceivedSerial(x => x.Contains("+JOIN: LoRaWAN modem is busy", StringComparison.Ordinal)))
                        break;
                    else if (ReceivedSerial(x => x.Contains("+JOIN: Join failed", StringComparison.Ordinal)))
                        break;

                    // wait a bit to not starve CPU, still waiting for a response from serial port
                    await Task.Delay(50);
                }

                await Task.Delay(timeoutPerTry);

                // check serial log again before sending another request
                if (ReceivedSerial((s) => s.StartsWith("+JOIN: Network joined", StringComparison.Ordinal)))
                    return true;
            }

            return false;
        }

        public LoRaArduinoSerial setDeviceLowPower()
        {
            this.sendCommand("AT+LOWPOWER\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial setDeviceReset()
        {
            this.sendCommand("AT+RESET\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task setDeviceResetAsync()
        {
            this.sendCommand("AT+RESET\r\n");

            await Task.Delay(DEFAULT_TIMEWAIT);

            await EnsureSerialAnswerAsync("+RESET:", 10);
        }

        /// <summary>
        /// Reset the device to default, usefull for port manipulation.
        /// </summary>
        public async Task setDeviceDefaultAsync()
        {
            try
            {
                this.sendCommand("AT+FDEFAULT=RISINGHF\r\n");
            }
#pragma warning disable CA1031 // Do not catch general exception types.
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                TestLogger.Log($"Error during {nameof(this.setDeviceModeAsync)}. {ex}");
            }

            await EnsureSerialAnswerAsync("+FDEFAULT:", 10);
        }

        private short getBatteryVoltage()
        {
            short battery = 0;

            // pinMode(CHARGE_STATUS_PIN, OUTPUT);
            // digitalWrite(CHARGE_STATUS_PIN, LOW);
            // Thread.Sleep(DEFAULT_TIMEWAIT);
            // battery = (analogRead(BATTERY_POWER_PIN) * 3300 * 11) >> 10;
            // pinMode(CHARGE_STATUS_PIN, INPUT);
            return battery;
        }

        public void sendCommand(string command)
        {
            try
            {
                this.serialPort.Write(command);

                if (LogWrites)
                    TestLogger.Log($"[SERIALW] {command}");
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error writing to serial port. {ex}");
                throw;
            }
        }

        public void Dispose()
        {
            this.serialPort.DataReceived -= OnSerialDeviceData;
            this.serialPort.Close();
            this.serialPort = null;
            GC.SuppressFinalize(this);
        }

        private async Task EnsureSerialAnswerAsync(string expectedSerialStartText, int retries = 10, [CallerMemberName] string memberName = "")
        {
            for (var i = 0; i < retries; i++)
            {
                if (ReceivedSerial(x => x.StartsWith(expectedSerialStartText, StringComparison.InvariantCultureIgnoreCase)))
                {
                    await Task.Delay(DEFAULT_TIMEWAIT);
                    return;
                }

                await Task.Delay(DEFAULT_TIMEWAIT);
            }

            throw new ArduinoDeviceFailedException($"Waited for command {memberName}, but could not complete in the allocated time.");
        }
    }
}
