// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using LoRaWan.Test.Shared;
    using Newtonsoft.Json.Linq;
    using Xunit;

    // Tests OTAA requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class MacTest : IntegrationTestBaseCi
    {
        public MacTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        // Send a LinkCheckCmd from the device and expect an answer.
        // Use Device22_ABP
        [Fact]
        public async Task Test_Device_Initiated_Mac_LinkCheckCmd_Should_work()
        {
            const int MESSAGES_COUNT = 3;

            var device = this.TestFixtureCi.Device22_ABP;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);
            await this.ArduinoDevice.setPortAsync(0);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);
            for (var i = 0; i < MESSAGES_COUNT; ++i)
            {
                var msg = "02";
                this.Log($"{device.DeviceID}: Sending unconfirmed Mac LinkCheckCmd message");
                await this.ArduinoDevice.transferHexPacketAsync(msg, 10);
                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSGHEX: Done", this.ArduinoDevice.SerialLogs);

                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: LinkCheckCmd mac command detected in upstream payload:");
            }

            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: Answering to a");

            this.TestFixtureCi.ClearLogs();
        }

        // Ensures that Mac Commands C2D messages working
        // Uses Device23_OTAA
        [Theory]
        [InlineData("")]
        [InlineData("test")]
        public async Task Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_Commands(string c2dMessageBody)
        {
            var device = this.TestFixtureCi.Device10_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // Sends 2x confirmed messages
            for (var i = 1; i <= 2; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            await this.TestFixtureCi.SendCloudToDeviceMessage(device.DeviceID, c2dMessageBody, new Dictionary<string, string> { { Constants.FPORT_MSG_PROPERTY_KEY, "1" } });
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;

            // Sends 3x confirmed messages, stopping if C2D message is found
            for (var i = 3; i <= 6; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                var c2dLogMessage = $"{device.DeviceID}: Cloud to device MAC command DevStatusCmd received";
                var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(c2dLogMessage);
                    },
                    new SearchLogOptions
                    {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    this.Log($"{device.DeviceID}: Found Mac C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                }

                var macCommandMessage = $"{device.DevAddr}: DevStatusCmd mac command detected in upstream payload: Battery Level";
                var macSearchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                    (messageBody) =>
                    {
                        return messageBody.StartsWith(macCommandMessage);
                    },
                    new SearchLogOptions
                    {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (macSearchResults.Found)
                {
                    this.Log($"{device.DeviceID}: Found Mac Command reply in log (after sending {i}/10) ? {foundC2DMessage}");
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }
        }

        private string ToHexString(string str)
        {
            var sb = new StringBuilder();

            var bytes = Encoding.UTF8.GetBytes(str);
            foreach (var t in bytes)
            {
                sb.Append(t.ToString("X2"));
            }

            return sb.ToString(); // returns: "48656C6C6F20776F726C64" for "Hello world"
        }
    }
}