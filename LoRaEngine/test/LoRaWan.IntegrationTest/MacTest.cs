// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using LoRaTools;
    using LoRaTools.CommonAPI;
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
            try
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
                    await Task.Delay(2 * Constants.DELAY_BETWEEN_MESSAGES);

                    // After transferPacket: Expectation from serial
                    // +MSG: Done
                    await AssertUtils.ContainsWithRetriesAsync("+MSGHEX: Done", this.ArduinoDevice.SerialLogs);

                    await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                    await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: LinkCheckCmd mac command detected in upstream payload:");
                }

                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: answering to a");
            }
            finally
            {
                await this.ArduinoDevice.setPortAsync(1);
            }

            this.TestFixtureCi.ClearLogs();
        }

        // Ensures that Mac Commands C2D messages working
        // Uses Device23_OTAA
        [Theory]
        [InlineData("Device23_OTAA")]
        [InlineData("Device23_OTAA_MultiGw")]
        public async Task Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_Commands(string devicePropertyName)
        {
            var device = this.TestFixtureCi.GetDeviceByPropertyName(devicePropertyName);
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            if (device.IsMultiGw)
            {
                await this.TestFixtureCi.WaitForTwinSyncAfterJoinAsync(this.ArduinoDevice.SerialLogs, device.DeviceID);
            }

            await this.Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_CommandsImplAsync(device, "test");
            await this.Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_CommandsImplAsync(device, string.Empty);
        }

        private async Task Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_CommandsImplAsync(TestDeviceInfo device, string c2dMessageBody)
        {
            const int MaxAttempts = 5;
            const int UnconfirmedMsgCount = 2;

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= UnconfirmedMsgCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{UnconfirmedMsgCount}");

                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                this.TestFixtureCi.ClearLogs();
            }

            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = 1,
                Payload = c2dMessageBody,
                MacCommands = new MacCommand[]
                {
                    new DevStatusRequest(),
                }
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var macCommandReceivedMsg = $"{device.DeviceID}: cloud to device MAC command DevStatusCmd received";
            var foundMacCommandReceivedMsg = false;

            var deviceMacCommandResponseMsg = $": DevStatusCmd mac command detected in upstream payload: Type: DevStatusCmd Answer, Battery Level:";
            var foundDeviceMacCommandResponseMsg = false;

            // Sends 5x unconfirmed messages, stopping if assertions succeeded
            for (var i = 1; i <= MaxAttempts; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{MaxAttempts}");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                if (!foundMacCommandReceivedMsg)
                {
                    var searchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                        (messageBody) =>
                        {
                            return messageBody.StartsWith(macCommandReceivedMsg);
                        },
                        new SearchLogOptions
                        {
                            Description = macCommandReceivedMsg,
                            MaxAttempts = 1
                        });

                    // We should only receive the message once
                    if (searchResults.Found)
                    {
                        foundMacCommandReceivedMsg = true;
                        this.Log($"{device.DeviceID}: Found Mac C2D message in log (after sending {i}/{MaxAttempts}) ? {foundMacCommandReceivedMsg}");
                    }
                }

                if (!foundDeviceMacCommandResponseMsg)
                {
                    var macSearchResults = await this.TestFixtureCi.SearchNetworkServerModuleAsync(
                        (messageBody) =>
                        {
                            return messageBody.Contains(deviceMacCommandResponseMsg, StringComparison.InvariantCultureIgnoreCase);
                        },
                        new SearchLogOptions
                        {
                            Description = deviceMacCommandResponseMsg,
                            MaxAttempts = 1
                        });

                    // We should only receive the message once
                    if (macSearchResults.Found)
                    {
                        foundDeviceMacCommandResponseMsg = true;
                        this.Log($"{device.DeviceID}: Found Mac Command reply in log (after sending {i}/{MaxAttempts}) ? {foundDeviceMacCommandResponseMsg}");
                    }
                }

                this.TestFixtureCi.ClearLogs();
                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                if (foundDeviceMacCommandResponseMsg && foundMacCommandReceivedMsg)
                    break;
            }

            Assert.True(foundMacCommandReceivedMsg, $"Did not find in network server logs: '{macCommandReceivedMsg}'");
            Assert.True(foundDeviceMacCommandResponseMsg, $"Did not find in network server logs: '{deviceMacCommandResponseMsg}'");
        }
    }
}