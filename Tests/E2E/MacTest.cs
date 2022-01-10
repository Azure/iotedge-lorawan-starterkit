// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.Tests.E2E
{
    using System;
    using System.Globalization;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.CommonAPI;
    using LoRaWan.Tests.Common;
    using Xunit;
    using XunitRetryHelper;

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
        [RetryFact]
        public async Task Test_Device_Initiated_Mac_LinkCheckCmd_Should_work()
        {
            try
            {
                const int MESSAGES_COUNT = 3;

                var device = TestFixtureCi.Device22_ABP;
                LogTestStart(device);
                await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
                await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
                await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);
                await ArduinoDevice.setPortAsync(0);

                await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);
                for (var i = 0; i < MESSAGES_COUNT; ++i)
                {
                    var msg = "02";
                    Log($"{device.DeviceID}: Sending unconfirmed Mac LinkCheckCmd message");
                    await ArduinoDevice.transferHexPacketAsync(msg, 10);
                    await Task.Delay(2 * Constants.DELAY_BETWEEN_MESSAGES);

                    // After transferPacket: Expectation from serial
                    // +MSG: Done
                    await AssertUtils.ContainsWithRetriesAsync("+MSGHEX: Done", ArduinoDevice.SerialLogs);

                    await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                    await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: LinkCheckCmd mac command detected in upstream payload:");
                }

                await TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: answering to a");
            }
            finally
            {
                await ArduinoDevice.setPortAsync(1);
            }

            TestFixtureCi.ClearLogs();
        }

        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_Commands_Single()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device23_OTAA));
            LogTestStart(device);
            return Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_Commands(device);
        }

        [RetryFact]
        public Task Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_Commands_MultiGw()
        {
            var device = TestFixtureCi.GetDeviceByPropertyName(nameof(TestFixtureCi.Device23_OTAA_MultiGw));
            LogTestStart(device);
            return Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_Commands(device);
        }

        // Ensures that Mac Commands C2D messages working
        // Uses Device23_OTAA
        private async Task Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_Commands(TestDeviceInfo device)
        {

            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration);

            var joinSucceeded = await ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded, "Join failed");

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            if (device.IsMultiGw)
            {
                await TestFixtureCi.WaitForTwinSyncAfterJoinAsync(ArduinoDevice.SerialLogs, device.DeviceID);
            }

            await Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_CommandsImplAsync(device, "test");
            await Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_CommandsImplAsync(device, string.Empty);
        }

        private async Task Test_OTAA_Unconfirmed_Send_And_Receive_C2D_Mac_CommandsImplAsync(TestDeviceInfo device, string c2dMessageBody)
        {
            const int MaxAttempts = 5;
            const int UnconfirmedMsgCount = 2;

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= UnconfirmedMsgCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{UnconfirmedMsgCount}");

                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                TestFixtureCi.ClearLogs();
            }

            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = FramePorts.App1,
                Payload = c2dMessageBody,
                MacCommands = { new DevStatusRequest() }
            };

            await TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var macCommandReceivedMsg = $"{device.DeviceID}: cloud to device MAC command DevStatusCmd received";
            var foundMacCommandReceivedMsg = false;

            var deviceMacCommandResponseMsg = $": DevStatusCmd mac command detected in upstream payload: Type: DevStatusCmd Answer, Battery Level:";
            var foundDeviceMacCommandResponseMsg = false;

            // Sends 5x unconfirmed messages, stopping if assertions succeeded
            for (var i = 1; i <= MaxAttempts; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{MaxAttempts}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // check if c2d message was found
                if (!foundMacCommandReceivedMsg)
                {
                    var searchResults = await TestFixtureCi.SearchNetworkServerModuleAsync(
                        (messageBody) =>
                        {
                            return messageBody.StartsWith(macCommandReceivedMsg, StringComparison.Ordinal);
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
                        Log($"{device.DeviceID}: Found Mac C2D message in log (after sending {i}/{MaxAttempts}) ? {foundMacCommandReceivedMsg}");
                    }
                }

                if (!foundDeviceMacCommandResponseMsg)
                {
                    var macSearchResults = await TestFixtureCi.SearchNetworkServerModuleAsync(
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
                        Log($"{device.DeviceID}: Found Mac Command reply in log (after sending {i}/{MaxAttempts}) ? {foundDeviceMacCommandResponseMsg}");
                    }
                }

                TestFixtureCi.ClearLogs();
                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                if (foundDeviceMacCommandResponseMsg && foundMacCommandReceivedMsg)
                    break;
            }

            Assert.True(foundMacCommandReceivedMsg, $"Did not find in network server logs: '{macCommandReceivedMsg}'");
            Assert.True(foundDeviceMacCommandResponseMsg, $"Did not find in network server logs: '{deviceMacCommandResponseMsg}'");
        }

        /// <summary>
        /// Ensures that data rate is updated on a device after a LinkADRCmd MAC Command is sent as C2D message.
        /// </summary>
        [RetryFact]
        public async Task Data_Rate_Is_Updated_When_C2D_With_LinkADRCmd_Received()
        {
            const int messagesToSend = 10;
            const int warmUpMessageCount = 2;

            var device = TestFixtureCi.Device32_ABP;
            LogTestStart(device);

            // Setup LoRa device properties
            await ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            // Setup protocol properties
            // Start with DR5
            await ArduinoDevice.SetupLora(TestFixtureCi.Configuration.LoraRegion, LoRaArduinoSerial._data_rate_t.DR5, 4, true);
            await TestFixture.CleanupC2DDeviceQueueAsync(device.DeviceID);

            // Sends 2x unconfirmed messages
            for (var i = 1; i <= warmUpMessageCount; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);
                TestFixture.ClearLogs();
            }

            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Fport = FramePort.MacCommand,
                Payload = string.Empty,
                MacCommands = { new LinkADRRequest(datarate: 3, txPower: 4, chMask: 25, chMaskCntl: 0, nbTrans: 1) } // Update data rate to DR3
            };

            await TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            Log($"C2D Message sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundLinkADRCmd = false;
            var foundChangedDataRate = false;

            // Sends 8x unconfirmed messages, stopping if C2D message is found and data rate is updated
            for (var i = warmUpMessageCount + 1; i <= messagesToSend; ++i)
            {
                var msg = PayloadGenerator.Next().ToString(CultureInfo.InvariantCulture);
                Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/{messagesToSend}");
                await ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", ArduinoDevice.SerialLogs);

                // check if C2D message was found
                if (await SearchMessageAsync($"{device.DeviceID}: cloud to device MAC command LinkADRCmd received Type: LinkADRCmd Answer, datarate: 3"))
                    foundC2DMessage = true;

                // check if LinkADRCmd MAC Command was detected
                if (await SearchMessageAsync("LinkADRCmd mac command detected in upstream payload: Type: LinkADRCmd Answer, power: changed, data rate: changed"))
                    foundLinkADRCmd = true;

                // check if the data rate was changed to DR3
                if (await SearchMessageAsync("\"datr\":\"SF9BW125\""))
                    foundChangedDataRate = true;

                async Task<bool> SearchMessageAsync(string message)
                {
                    var searchResult =
                        await TestFixtureCi.SearchNetworkServerModuleAsync(messageBody => messageBody.Contains(message, StringComparison.OrdinalIgnoreCase),
                                                                           new SearchLogOptions
                                                                           {
                                                                               Description = message,
                                                                               MaxAttempts = 1
                                                                           });

                    return searchResult.Found;
                }

                TestFixture.ClearLogs();
                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                if (foundC2DMessage && foundLinkADRCmd && foundChangedDataRate)
                    break;
            }

            Assert.True(foundC2DMessage, $"Could not find C2D message with MAC command LinkADRCmd in LNS log");
            Assert.True(foundLinkADRCmd, $"Could not find LinkADRCmd MAC Command in LNS log");
            Assert.True(foundChangedDataRate, $"Could not find updated data rate in LNS log");
        }
    }
}
