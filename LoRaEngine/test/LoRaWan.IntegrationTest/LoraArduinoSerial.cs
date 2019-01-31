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

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO.Ports;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;

    public sealed partial class LoRaArduinoSerial : IDisposable
    {
        private const int DEFAULT_TIMEWAIT = 100;
        private SerialPort serialPortWin;
        private SerialDevice serialPort;

        public enum Class_Type_T
        {
            CLASS_A = 0,
            CLASS_C
        }

        public enum Physical_Type_T
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

        public enum Device_Mode_T
        {
            LWABP = 0,
            LWOTAA,
            TEST
        }

        public enum Otaa_Join_Cmd_T
        {
            JOIN = 0,
            FORCE
        }

        public enum Window_Delay_T
        {
            RECEIVE_DELAY1 = 0,
            RECEIVE_DELAY2,
            JOIN_ACCEPT_DELAY1,
            JOIN_ACCEPT_DELAY2
        }

        public enum Band_Width_T
        {
            BW125 = 125,
            BW250 = 250,
            BW500 = 500
        }

        public enum Spreading_Factor_T
        {
            SF12 = 12,
            SF11 = 11,
            SF10 = 10,
            SF9 = 9,
            SF8 = 8,
            SF7 = 7
        }

        public enum Data_Rate_T
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

        // Creates a new instance based on port identifier
        private LoRaArduinoSerial(SerialDevice serialPort)
        {
            this.serialPort = serialPort;

            this.serialPort.DataReceived += this.OnSerialDeviceData;
        }

        private void OnSerialDeviceData(object arg1, byte[] data)
        {
            try
            {
                var dataread = System.Text.Encoding.UTF8.GetString(data);
                this.OnSerialDataReceived(dataread);
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error in serial data rx. {ex.ToString()}");
            }
        }

        private byte[] windowsSerialPortBuffer = null;

        private LoRaArduinoSerial(SerialPort sp)
        {
            this.serialPortWin = sp;
            this.windowsSerialPortBuffer = new byte[this.serialPortWin.ReadBufferSize];

            this.serialPortWin.DataReceived += this.OnSerialDeviceDataForWindows;
        }

        private void OnSerialDeviceDataForWindows(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var myserialPort = (SerialPort)sender;
                var readCount = myserialPort.Read(this.windowsSerialPortBuffer, 0, this.windowsSerialPortBuffer.Length);
                var dataread = System.Text.Encoding.UTF8.GetString(this.windowsSerialPortBuffer, 0, readCount);
                this.OnSerialDataReceived(dataread);
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error in serial data rx. {ex.ToString()}");
            }
        }

        private void OnSerialDataReceived(string rawData)
        {
            try
            {
                var data = rawData.Replace("\r", string.Empty);

                var lines = string.Concat(this.buff, data).Split('\n');
                this.buff = string.Empty;

                if (lines.Length > 0)
                {
                    for (var i = 0; i < lines.Length - 1; i++)
                    {
                        if (!string.IsNullOrEmpty(lines[i]))
                        {
                            this.AppendSerialLog(lines[i]);
                        }
                    }

                    // last line: does the input ends with a new line?
                    var lastParsedLine = lines[lines.Length - 1];
                    if (!string.IsNullOrEmpty(lastParsedLine))
                    {
                        if (data.EndsWith('\n'))
                        {
                            // add as finished line
                            this.AppendSerialLog(lastParsedLine);
                        }
                        else
                        {
                            // buffer it for next line
                            this.buff = lastParsedLine;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error processing serial data. {ex.ToString()}");
            }
        }

        private ConcurrentQueue<string> serialLogs = new ConcurrentQueue<string>();
        private string buff = string.Empty;

        private void AppendSerialLog(string message)
        {
            TestLogger.Log($"[SERIAL] {message}");
            this.serialLogs.Enqueue(message);
        }

        public IReadOnlyCollection<string> SerialLogs
        {
            get { return this.serialLogs; }
        }

        // Gets/sets if serial writes should be logged
        // Disabled by default
        public bool LogWrites { get; set; }

        public void ClearSerialLogs()
        {
            TestLogger.Log($"*** Clearing serial logs ({this.serialLogs.Count}) ***");
            this.serialLogs.Clear();
        }

        public LoRaArduinoSerial SetId(string devAddr, string devEUI, string appEUI)
        {
            if (!string.IsNullOrEmpty(devAddr))
            {
                string cmd = $"AT+ID=DevAddr,{devAddr}\r\n";
                this.SendCommand(cmd);

                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            if (!string.IsNullOrEmpty(devEUI))
            {
                string cmd = $"AT+ID=DevEui,{devEUI}\r\n";
                this.SendCommand(cmd);
                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            if (!string.IsNullOrEmpty(appEUI))
            {
                string cmd = $"AT+ID=AppEui,{appEUI}\r\n";
                this.SendCommand(cmd);
                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            return this;
        }

        public async Task SetIdAsync(string devAddr, string devEUI, string appEUI)
        {
            try
            {
                if (!string.IsNullOrEmpty(devAddr))
                {
                    string cmd = $"AT+ID=DevAddr,{devAddr}\r\n";
                    this.SendCommand(cmd);

                    await Task.Delay(DEFAULT_TIMEWAIT);
                }

                if (!string.IsNullOrEmpty(devEUI))
                {
                    string cmd = $"AT+ID=DevEui,{devEUI}\r\n";
                    this.SendCommand(cmd);
                    await Task.Delay(DEFAULT_TIMEWAIT);
                }

                if (!string.IsNullOrEmpty(appEUI))
                {
                    string cmd = $"AT+ID=AppEui,{appEUI}\r\n";
                    this.SendCommand(cmd);
                    await Task.Delay(DEFAULT_TIMEWAIT);
                }
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error during {nameof(this.SetIdAsync)}. {ex.ToString()}");
            }
        }

        public LoRaArduinoSerial SetKey(string nwkSKey, string appSKey, string appKey)
        {
            if (!string.IsNullOrEmpty(nwkSKey))
            {
                string cmd = $"AT+KEY=NWKSKEY,{nwkSKey}\r\n";
                this.SendCommand(cmd);
                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            if (!string.IsNullOrEmpty(appSKey))
            {
                string cmd = $"AT+KEY=APPSKEY,{appSKey}\r\n";
                this.SendCommand(cmd);

                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            if (!string.IsNullOrEmpty(appKey))
            {
                string cmd = $"AT+KEY= APPKEY,{appKey}\r\n";
                this.SendCommand(cmd);

                Thread.Sleep(DEFAULT_TIMEWAIT);
            }

            return this;
        }

        public async Task SetKeyAsync(string nwkSKey, string appSKey, string appKey)
        {
            try
            {
                if (!string.IsNullOrEmpty(nwkSKey))
                {
                    string cmd = $"AT+KEY=NWKSKEY,{nwkSKey}\r\n";
                    this.SendCommand(cmd);
                    await Task.Delay(DEFAULT_TIMEWAIT);
                }

                if (!string.IsNullOrEmpty(appSKey))
                {
                    string cmd = $"AT+KEY=APPSKEY,{appSKey}\r\n";
                    this.SendCommand(cmd);

                    await Task.Delay(DEFAULT_TIMEWAIT);
                }

                if (!string.IsNullOrEmpty(appKey))
                {
                    string cmd = $"AT+KEY= APPKEY,{appKey}\r\n";
                    this.SendCommand(cmd);

                    await Task.Delay(DEFAULT_TIMEWAIT);
                }
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error during {nameof(this.SetKeyAsync)}. {ex.ToString()}");
            }
        }

        public LoRaArduinoSerial SetDataRate(Data_Rate_T dataRate, Physical_Type_T physicalType)
        {
            if (physicalType == Physical_Type_T.EU434)
                this.SendCommand("AT+DR=EU433\r\n");
            else if (physicalType == Physical_Type_T.EU868)
                this.SendCommand("AT+DR=EU868\r\n");
            else if (physicalType == Physical_Type_T.US915)
                this.SendCommand("AT+DR=US915\r\n");
            else if (physicalType == Physical_Type_T.US915HYBRID)
                this.SendCommand("AT+DR=US915HYBRID\r\n");
            else if (physicalType == Physical_Type_T.AU915)
                this.SendCommand("AT+DR=AU915\r\n");
            else if (physicalType == Physical_Type_T.AU915OLD)
                this.SendCommand("AT+DR=AU915OLD\r\n");
            else if (physicalType == Physical_Type_T.CN470)
                this.SendCommand("AT+DR=CN470\r\n");
            else if (physicalType == Physical_Type_T.CN779)
                this.SendCommand("AT+DR=CN779\r\n");
            else if (physicalType == Physical_Type_T.AS923)
                this.SendCommand("AT+DR=AS923\r\n");
            else if (physicalType == Physical_Type_T.KR920)
                this.SendCommand("AT+DR=KR920\r\n");
            else if (physicalType == Physical_Type_T.IN865)
                this.SendCommand("AT+DR=IN865\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            string cmd = $"AT+DR={dataRate}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetDataRateAsync(Data_Rate_T dataRate, Physical_Type_T physicalType)
        {
            if (physicalType == Physical_Type_T.EU434)
                this.SendCommand("AT+DR=EU433\r\n");
            else if (physicalType == Physical_Type_T.EU868)
                this.SendCommand("AT+DR=EU868\r\n");
            else if (physicalType == Physical_Type_T.US915)
                this.SendCommand("AT+DR=US915\r\n");
            else if (physicalType == Physical_Type_T.US915HYBRID)
                this.SendCommand("AT+DR=US915HYBRID\r\n");
            else if (physicalType == Physical_Type_T.AU915)
                this.SendCommand("AT+DR=AU915\r\n");
            else if (physicalType == Physical_Type_T.AU915OLD)
                this.SendCommand("AT+DR=AU915OLD\r\n");
            else if (physicalType == Physical_Type_T.CN470)
                this.SendCommand("AT+DR=CN470\r\n");
            else if (physicalType == Physical_Type_T.CN779)
                this.SendCommand("AT+DR=CN779\r\n");
            else if (physicalType == Physical_Type_T.AS923)
                this.SendCommand("AT+DR=AS923\r\n");
            else if (physicalType == Physical_Type_T.KR920)
                this.SendCommand("AT+DR=KR920\r\n");
            else if (physicalType == Physical_Type_T.IN865)
                this.SendCommand("AT+DR=IN865\r\n");

            await Task.Delay(DEFAULT_TIMEWAIT);

            string cmd = $"AT+DR={dataRate}\r\n";
            this.SendCommand(cmd);

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial SetPower(short power)
        {
            string cmd = $"AT+POWER={power}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetPowerAsync(short power)
        {
            string cmd = $"AT+POWER={power}\r\n";
            this.SendCommand(cmd);

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial SetPort(int port)
        {
            string cmd = "AT+PORT={port}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial SetAdaptiveDataRate(bool command)
        {
            if (command)
                this.SendCommand("AT+ADR=ON\r\n");
            else
                this.SendCommand("AT+ADR=OFF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetAdaptiveDataRateAsync(bool command)
        {
            if (command)
                this.SendCommand("AT+ADR=ON\r\n");
            else
                this.SendCommand("AT+ADR=OFF\r\n");

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        // Wait until the serial data is empty
        internal async Task<bool> WaitForIdleAsync(TimeSpan? timeout = null)
        {
            var timeoutToUse = timeout ?? TimeSpan.FromSeconds(60);
            var delayTime = timeoutToUse / 2;
            var start = DateTime.UtcNow;

            do
            {
                this.ClearSerialLogs();
                await Task.Delay(delayTime);

                if (!this.SerialLogs.Any())
                    return true;
            }
            while (start.Subtract(DateTime.UtcNow) <= timeoutToUse);

            return false;
        }

        public LoRaArduinoSerial SetChannel(int channel, float frequency)
        {
            string cmd = $"AT+CH={channel},{(short)frequency}.{(short)(frequency * 10) % 10}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetChannelAsync(int channel, float frequency)
        {
            string cmd = $"AT+CH={channel},{(short)frequency}.{(short)(frequency * 10) % 10}\r\n";
            this.SendCommand(cmd);

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial SetChannel(char channel, float frequency, Data_Rate_T dataRata)
        {
            string cmd = $"AT+CH={channel},{(short)frequency}.{(short)(frequency * 10) % 10},{dataRata}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial SetChannel(char channel, float frequency, Data_Rate_T dataRataMin, Data_Rate_T dataRataMax)
        {
            string cmd = $"AT+CH={channel},{(short)frequency}.{(short)(frequency * 10) % 10},{dataRataMin},{dataRataMax}\r\n";

            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task<bool> TransferPacketAsync(string buffer, int timeout)
        {
            try
            {
                this.SendCommand("AT+MSG=\"");

                this.SendCommand(buffer);

                this.SendCommand("\"\r\n");

                DateTime start = DateTime.Now;

                while (true)
                {
                    if (this.ReceivedSerial(x => x.StartsWith("+MSG: Done")))
                        return true;
                    else if (start.AddSeconds(timeout) < DateTime.Now)
                        return false;

                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error during {nameof(this.TransferPacketAsync)}. {ex.ToString()}");
                return false;
            }
        }

        public bool TransferPacket(string buffer, int timeout)
        {
            this.SendCommand("AT+MSG=\"");

            this.SendCommand(buffer);

            this.SendCommand("\"\r\n");

            DateTime start = DateTime.Now;

            while (true)
            {
                if (this.ReceivedSerial(x => x.StartsWith("+MSG: Done")))
                    return true;
                else if (start.AddSeconds(timeout) < DateTime.Now)
                    return false;
            }
        }

        private bool ReceivedSerial(Func<string, bool> predicate)
        {
            foreach (var serialLine in this.SerialLogs)
            {
                if (predicate(serialLine))
                    return true;
            }

            return false;
        }

        public bool TransferPacketWithConfirmed(string buffer, int timeout)
        {
            this.SendCommand("AT+CMSG=\"");
            this.SendCommand(buffer);
            this.SendCommand("\"\r\n");

            DateTime start = DateTime.Now;

            while (true)
            {
                if (this.ReceivedSerial(x => x.StartsWith("+CMSG: ACK Received")))
                    return true;
                else if (start.AddSeconds(timeout) < DateTime.Now)
                    return false;
            }

            // if (_buffer.Contains("+CMSG: ACK Received")) return true;
        }

        public async Task<bool> TransferPacketWithConfirmedAsync(string buffer, int timeout)
        {
            try
            {
                this.SendCommand("AT+CMSG=\"");
                this.SendCommand(buffer);
                this.SendCommand("\"\r\n");

                DateTime start = DateTime.Now;

                while (true)
                {
                    if (this.ReceivedSerial(x => x.StartsWith("+CMSG: ACK Received")))
                        return true;
                    else if (start.AddSeconds(timeout) < DateTime.Now)
                        return false;

                    await Task.Delay(100);
                }

                // if (_buffer.Contains("+CMSG: ACK Received")) return true;
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error during {nameof(this.TransferPacketWithConfirmedAsync)}. {ex.ToString()}");
                return false;
            }
        }

        public LoRaArduinoSerial SetUnconfirmedMessageRepeatTime(uint time)
        {
            if (time > 15)
                time = 15;
            else if (time == 0)
                time = 1;

            string cmd = $"AT+REPT={time}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial SetConfirmedMessageRetryTime(uint time)
        {
            if (time > 15)
                time = 15;
            else if (time == 0)
                time = 1;

            string cmd = $"AT+RETRY={time}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetConfirmedMessageRetryTimeAsync(uint time)
        {
            if (time > 15)
                time = 15;
            else if (time == 0)
                time = 1;

            string cmd = $"AT+RETRY={time}\r\n";
            this.SendCommand(cmd);

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial SetReceiceWindowFirst(bool command)
        {
            if (command)
                this.SendCommand("AT+RXWIN1=ON\r\n");
            else
                this.SendCommand("AT+RXWIN1=OFF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial SetReceiceWindowFirst(int channel, float frequency)
        {
            string cmd = $"AT+RXWIN1={channel},{(short)frequency}.{(short)(frequency * 10) % 10}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetReceiceWindowFirstAsync(int channel, float frequency)
        {
            string cmd = $"AT+RXWIN1={channel},{(short)frequency}.{(short)(frequency * 10) % 10}\r\n";
            this.SendCommand(cmd);

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial SetReceiceWindowSecond(float frequency, Data_Rate_T dataRate)
        {
            string cmd = $"AT+RXWIN2={(short)frequency}.{(short)(frequency * 10) % 10},{dataRate}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetReceiceWindowSecondAsync(float frequency, Data_Rate_T dataRate)
        {
            string cmd = $"AT+RXWIN2={(short)frequency}.{(short)(frequency * 10) % 10},{dataRate}\r\n";
            this.SendCommand(cmd);

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial SetReceiceWindowSecond(float frequency, Spreading_Factor_T spreadingFactor, Band_Width_T bandwidth)
        {
            string cmd = $"AT+RXWIN2={(short)frequency}.{(short)(frequency * 10) % 10},{spreadingFactor},{bandwidth}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial SetDutyCycle(bool command)
        {
            if (command)
                this.SendCommand("AT+LW=DC, ON\r\n");
            else
                this.SendCommand("AT+LW=DC, OFF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetDutyCycleAsync(bool command)
        {
            if (command)
                this.SendCommand("AT+LW=DC, ON\r\n");
            else
                this.SendCommand("AT+LW=DC, OFF\r\n");

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial SetJoinDutyCycle(bool command)
        {
            if (command)
                this.SendCommand("AT+LW=JDC,ON\r\n");
            else
                this.SendCommand("AT+LW=JDC,OFF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);
            return this;
        }

        public async Task SetJoinDutyCycleAsync(bool command)
        {
            if (command)
                this.SendCommand("AT+LW=JDC,ON\r\n");
            else
                this.SendCommand("AT+LW=JDC,OFF\r\n");

            await Task.Delay(DEFAULT_TIMEWAIT);
        }

        public LoRaArduinoSerial SetReceiceWindowDelay(Window_Delay_T command, short delay)
        {
            string cmd = string.Empty;

            if (command == Window_Delay_T.RECEIVE_DELAY1)
                cmd = $"AT+DELAY=RX1,{delay}\r\n";
            else if (command == Window_Delay_T.RECEIVE_DELAY2)
                cmd = $"AT+DELAY=RX2,{delay}\r\n";
            else if (command == Window_Delay_T.JOIN_ACCEPT_DELAY1)
                cmd = $"AT+DELAY=JRX1,{delay}\r\n";
            else if (command == Window_Delay_T.JOIN_ACCEPT_DELAY2)
                cmd = $"AT+DELAY=JRX2,{delay}\r\n";
            this.SendCommand(cmd);

            Thread.Sleep(DEFAULT_TIMEWAIT);
            return this;
        }

        public LoRaArduinoSerial SetClassType(Class_Type_T type)
        {
            if (type == Class_Type_T.CLASS_A)
                this.SendCommand("AT+CLASS=A\r\n");
            else if (type == Class_Type_T.CLASS_C)
                this.SendCommand("AT+CLASS=C\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial SetDeciveMode(Device_Mode_T mode)
        {
            if (mode == Device_Mode_T.LWABP)
                this.SendCommand("AT+MODE=LWABP\r\n");
            else if (mode == Device_Mode_T.LWOTAA)
                this.SendCommand("AT+MODE=LWOTAA\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public async Task SetDeviceModeAsync(Device_Mode_T mode)
        {
            try
            {
                if (mode == Device_Mode_T.LWABP)
                    this.SendCommand("AT+MODE=LWABP\r\n");
                else if (mode == Device_Mode_T.LWOTAA)
                    this.SendCommand("AT+MODE=LWOTAA\r\n");

                await Task.Delay(DEFAULT_TIMEWAIT);
             }
            catch (Exception ex)
            {
                TestLogger.Log($"Error during {nameof(this.SetDeviceModeAsync)}. {ex.ToString()}");
            }
        }

        public bool SetOTAAJoin(Otaa_Join_Cmd_T command, int timeout)
        {
            if (command == Otaa_Join_Cmd_T.JOIN)
                this.SendCommand("AT+JOIN\r\n");
            else if (command == Otaa_Join_Cmd_T.FORCE)
                this.SendCommand("AT+JOIN=FORCE\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            DateTime start = DateTime.Now;

            while (true)
            {
                if (this.ReceivedSerial(x => x.StartsWith("+JOIN: Done")))
                    return true;
                else if (this.ReceivedSerial(x => x.StartsWith("+JOIN: LoRaWAN modem is busy")))
                    return false;
                else if (this.ReceivedSerial(x => x.StartsWith("+JOIN: Join failed")))
                    return false;
                else if (start.AddMilliseconds(timeout) < DateTime.Now)
                    return false;
            }

            // ptr = _buffer.Contains("+JOIN: Join failed");
            // if (ptr) return false;
            // ptr = _buffer.Contains("+JOIN: LoRaWAN modem is busy");
            // if (ptr) return false;
        }

        public async Task<bool> SetOTAAJoinAsyncWithRetry(Otaa_Join_Cmd_T command, int timeoutPerTry, int retries)
        {
            for (var attempt = 1; attempt <= retries; ++attempt)
            {
                TestLogger.Log($"Join attempt #{attempt}/{retries}");
                if (command == Otaa_Join_Cmd_T.JOIN)
                    this.SendCommand("AT+JOIN\r\n");
                else if (command == Otaa_Join_Cmd_T.FORCE)
                    this.SendCommand("AT+JOIN=FORCE\r\n");

                await Task.Delay(DEFAULT_TIMEWAIT);

                DateTime start = DateTime.Now;

                while (DateTime.Now.Subtract(start).TotalMilliseconds < timeoutPerTry)
                {
                    if (this.ReceivedSerial((s) => s.Contains("+JOIN: Network joined", StringComparison.Ordinal)))
                        return true;
                    else if (this.ReceivedSerial(x => x.Contains("+JOIN: LoRaWAN modem is busy", StringComparison.Ordinal)))
                        break;
                    else if (this.ReceivedSerial(x => x.Contains("+JOIN: Join failed", StringComparison.Ordinal)))
                        break;

                    // wait a bit to not starve CPU, still waiting for a response from serial port
                    await Task.Delay(50);
                }

                await Task.Delay(timeoutPerTry);

                // check serial log again before sending another request
                if (this.ReceivedSerial((s) => s.StartsWith("+JOIN: Network joined", StringComparison.Ordinal)))
                    return true;
            }

            return false;
        }

        public LoRaArduinoSerial SetDeviceLowPower()
        {
            this.SendCommand("AT+LOWPOWER\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public LoRaArduinoSerial SetDeviceReset()
        {
            this.SendCommand("AT+RESET\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);

            return this;
        }

        public void SetDeviceDefault()
        {
            this.SendCommand("AT+FDEFAULT=RISINGHF\r\n");

            Thread.Sleep(DEFAULT_TIMEWAIT);
        }

        private short GetBatteryVoltage()
        {
            short battery = 0;

            // pinMode(CHARGE_STATUS_PIN, OUTPUT);
            // digitalWrite(CHARGE_STATUS_PIN, LOW);
            // Thread.Sleep(DEFAULT_TIMEWAIT);
            // battery = (analogRead(BATTERY_POWER_PIN) * 3300 * 11) >> 10;
            // pinMode(CHARGE_STATUS_PIN, INPUT);
            return battery;
        }

        public void SendCommand(string command)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    this.serialPort.Write(Encoding.UTF8.GetBytes(command));
                else
                    this.serialPortWin.Write(command);

                if (this.LogWrites)
                    TestLogger.Log($"[SERIALW] {command}");
            }
            catch (Exception ex)
            {
                TestLogger.Log($"Error writing to serial port. {ex.ToString()}");
                throw;
            }
        }

        public void Dispose()
        {
            if (this.serialPort != null)
            {
                this.serialPort.DataReceived -= this.OnSerialDeviceData;
                this.serialPort.Close();
                this.serialPort = null;
            }

            if (this.serialPortWin != null)
            {
                this.serialPortWin.DataReceived -= this.OnSerialDeviceDataForWindows;
                this.serialPortWin.Close();
                this.serialPortWin = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}