// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml;
    using LoRaWan.Test.Shared;
    using Newtonsoft.Json.Linq;
    using Xunit;

    // Tests OTAA requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class MultiGatewayTests : IntegrationTestBaseCi
    {
        public MultiGatewayTests(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        [Fact]
        public async Task Test_MultiGW_OTTA_Join_Single()
        {
            var device = this.TestFixtureCi.Device27_OTAA;
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            var joinConfirm = this.ArduinoDevice.SerialLogs.FirstOrDefault(s => s.StartsWith("+JOIN: NetID"));
            Assert.NotNull(joinConfirm);

            // validate that one GW refused the join
            const string joinRefusedMsg = "join refused";
            var joinRefused = await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync((s) => s.IndexOf(joinRefusedMsg) != -1, new SearchLogOptions(joinRefusedMsg));
            Assert.True(joinRefused.Found);
        }

        [Fact]
        public async Task Test_Deduplication_Drop()
        {
            var device = this.TestFixtureCi.Device28_ABP;
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            for (int i = 0; i < 10; i++)
            {
                var msg = PayloadGenerator.Next().ToString();
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                var allGwGotIt = await this.TestFixtureCi.ValidateMultiGatewaySources((log) => log.IndexOf("deduplication Strategy: Drop", StringComparison.OrdinalIgnoreCase) != -1);
                if (allGwGotIt)
                {
                    var notDuplicate = "\"IsDuplicate\":false";
                    var resultProcessed = await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync(logmsg => logmsg.IndexOf(notDuplicate) != -1, new SearchLogOptions(notDuplicate));
                    var resultDroped = await this.TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf("duplication strategy indicated to not process message") != -1);

                    Assert.NotNull(resultProcessed.MatchedEvent);
                    Assert.NotNull(resultDroped.MatchedEvent);

                    Assert.NotEqual(resultProcessed.MatchedEvent.SourceId, resultDroped.MatchedEvent.SourceId);
                }

                this.TestFixtureCi.ClearLogs();
            }
        }

        [Fact]
        public async Task Test_Deduplication_Mark()
        {
            var device = this.TestFixtureCi.Device29_ABP;
            this.LogTestStart(device);

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            for (int i = 0; i < 10; i++)
            {
                var msg = PayloadGenerator.Next().ToString();
                await this.ArduinoDevice.transferPacketAsync(msg, 10);
                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                var allGwGotIt = await this.TestFixtureCi.ValidateMultiGatewaySources((log) => log.IndexOf("deduplication Strategy: Mark", StringComparison.OrdinalIgnoreCase) != -1);
                if (allGwGotIt)
                {
                    var notDuplicate = "\"IsDuplicate\":false";
                    var isDuplicate = "\"IsDuplicate\":true";

                    var notDuplicateResult = await this.TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(notDuplicate) != -1);
                    var duplicateResult = await this.TestFixtureCi.SearchNetworkServerModuleAsync((s) => s.IndexOf(isDuplicate) != -1);

                    Assert.NotNull(notDuplicateResult.MatchedEvent);
                    Assert.NotNull(duplicateResult.MatchedEvent);

                    Assert.NotEqual(duplicateResult.MatchedEvent.SourceId, notDuplicateResult.MatchedEvent.SourceId);

                    // await this.TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, "\"dupmsg\":true");
                }

                this.TestFixtureCi.ClearLogs();
            }
        }

        [Fact]
        public async Task Test_OTAA_Unconfirmed_Receives_C2D_Message_Multi()
        {
            var device = this.TestFixtureCi.Device30_OTAA;
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

            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            var c2dMessage = new LoRaCloudToDeviceMessage()
            {
                Payload = c2dMessageBody,
                Fport = 1,
                MessageId = Guid.NewGuid().ToString(),
            };

            await this.TestFixtureCi.SendCloudToDeviceMessageAsync(device.DeviceID, c2dMessage);
            this.Log($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var expectedRxSerial = $"+MSG: PORT: 1; RX: \"{this.ToHexString(c2dMessageBody)}\"";
            this.Log($"Expected C2D received log is: {expectedRxSerial}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i = 3; i <= 10; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var c2dLogMessage = $"{device.DeviceID}: cloud to device message: {this.ToHexString(c2dMessageBody)}";
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
                    this.Log($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to Device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;
                }

                var localFoundCloudToDeviceInSerial = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }

                this.TestFixtureCi.ClearLogs();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (!foundReceivePacket)
            {
                foundReceivePacket = this.ArduinoDevice.SerialLogs.Contains(expectedRxSerial);
            }

            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
        }
    }
}