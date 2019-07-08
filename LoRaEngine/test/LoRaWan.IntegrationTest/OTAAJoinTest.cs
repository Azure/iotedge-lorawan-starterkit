// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;
    using Newtonsoft.Json;
    using Xunit;
    using Xunit.Sdk;

    // Tests OTAA join requests
    // OTAA joins requires the following information:
    // - DevEUI: a globally unique end-device identifier
    // - AppEUI: application identifier
    // - AppKey: a AES-128 key
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class OTAAJoinTest : IntegrationTestBaseCi
    {
        public OTAAJoinTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        // Ensures that an OTAA join will update the device twin
        // Uses Device1_OTAA
        [Fact]
        public async Task OTAA_Join_With_Valid_Device_Updates_DeviceTwin()
        {
            var device = this.TestFixtureCi.Device1_OTAA;
            this.LogTestStart(device);

            var twinBeforeJoin = await this.TestFixtureCi.GetTwinAsync(device.DeviceID);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            Assert.True(joinSucceeded, "Join failed");

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN);

            // After join: Expectation on serial
            // +JOIN: Network joined
            // +JOIN: NetID 010000 DevAddr 02:9B:0D:3E
            // Assert.Contains("+JOIN: Network joined", this.lora.SerialLogs);
            await AssertUtils.ContainsWithRetriesAsync(
                (s) => s.StartsWith("+JOIN: NetID", StringComparison.Ordinal),
                this.ArduinoDevice.SerialLogs);

            // verify status in device twin
            await Task.Delay(TimeSpan.FromSeconds(60));
            var twinAfterJoin = await this.TestFixtureCi.GetTwinAsync(device.DeviceID);
            Assert.NotNull(twinAfterJoin);
            Assert.NotNull(twinAfterJoin.Properties.Reported);
            try
            {
                Assert.True(twinAfterJoin.Properties.Reported.Contains("FCntUp"), "Property FCntUp does not exist");
                Assert.True(twinAfterJoin.Properties.Reported.Contains("FCntDown"), "Property FCntDown does not exist");
                Assert.True(twinAfterJoin.Properties.Reported.Contains("NetId"), "Property NetId does not exist");
                Assert.True(twinAfterJoin.Properties.Reported.Contains("DevAddr"), "Property DevAddr does not exist");
                Assert.True(twinAfterJoin.Properties.Reported.Contains("DevNonce"), "Property DevNonce does not exist");
                Assert.True(twinAfterJoin.Properties.Reported.Contains("NwkSKey"), "Property NwkSKey does not exist");
                Assert.True(twinAfterJoin.Properties.Reported.Contains("AppSKey"), "Property AppSKey does not exist");
                Assert.True(twinAfterJoin.Properties.Reported.Contains("DevEUI"), "Property DevEUI does not exist");
                var devAddrBefore = (string)twinBeforeJoin.Properties.Reported["DevAddr"];
                var devAddrAfter = (string)twinAfterJoin.Properties.Reported["DevAddr"];
                var actualReportedDevEUI = (string)twinAfterJoin.Properties.Reported["DevEUI"];
                Assert.NotEqual(devAddrAfter, devAddrBefore);
                Assert.Equal(device.DeviceID, actualReportedDevEUI);
                Assert.True(twinBeforeJoin.Properties.Reported.Version < twinAfterJoin.Properties.Reported.Version, "Twin was not updated after join");
                this.Log($"[INFO] Twin was updated successfully. Version changed from {twinBeforeJoin.Properties.Reported.Version} to {twinAfterJoin.Properties.Reported.Version}");
            }
            catch (XunitException xunitException)
            {
                if (this.TestFixtureCi.Configuration.IoTHubAssertLevel == LogValidationAssertLevel.Warning)
                {
                    this.Log($"[WARN] {nameof(this.OTAA_Join_With_Valid_Device_Updates_DeviceTwin)} failed. {xunitException.ToString()}");
                }
                else if (this.TestFixtureCi.Configuration.IoTHubAssertLevel == LogValidationAssertLevel.Error)
                {
                    throw;
                }
            }
        }

        // Ensure that a join with an invalid DevEUI fails
        // Does not need a real device, because the goal is no to have one that matches the DevEUI
        // Uses Device2_OTAA
        [Fact]
        public async Task OTAA_Join_With_Wrong_DevEUI_Fails()
        {
            var device = this.TestFixtureCi.Device2_OTAA;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 3);
            Assert.False(joinSucceeded, "Join suceeded for invalid DevEUI");

            await this.ArduinoDevice.WaitForIdleAsync();
        }

        // Ensure that a join with an invalid AppKey fails
        // Uses Device3_OTAA
        [Fact]
        public async Task OTAA_Join_With_Wrong_AppKey_Fails()
        {
            var device = this.TestFixtureCi.Device3_OTAA;
            this.LogTestStart(device);
            var appKeyToUse = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            Assert.NotEqual(appKeyToUse, device.AppKey);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, appKeyToUse);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 3);
            Assert.False(joinSucceeded, "Join suceeded for invalid AppKey (mic check should fail)");
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                $"{device.DeviceID}: join refused: invalid MIC",
                $"{device.DeviceID}: join request MIC invalid");

            await this.ArduinoDevice.WaitForIdleAsync();
        }

        // Ensure that a join with an invalid AppKey fails
        // Uses Device13_OTAA
        [Fact]
        public async Task OTAA_Join_With_Wrong_AppEUI_Fails()
        {
            var device = this.TestFixtureCi.Device13_OTAA;
            this.LogTestStart(device);

            var appEUIToUse = "FF7A00000000FCE3";
            Assert.NotEqual(appEUIToUse, device.AppEUI);
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, appEUIToUse);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 3);
            Assert.False(joinSucceeded, "Join suceeded for invalid AppKey");

            await this.ArduinoDevice.WaitForIdleAsync();
        }

        // Performs a OTAA join and sends 1 unconfirmed, 1 confirmed and rejoins
        [Theory]
        [InlineData("Device20_OTAA")]
        [InlineData("Device20_OTAA_MultiGw")]
        public async Task Test_OTAA_Join_Send_And_Rejoin_With_Custom_RX2_DR(string devicePropertyName)
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

            // Sends 1x unconfirmed messages
            this.TestFixtureCi.ClearLogs();

            var msg = PayloadGenerator.Next().ToString();
            await this.ArduinoDevice.transferPacketAsync(msg, 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

            // 0000000000000004: valid frame counter, msg: 1 server: 0
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

            // 0000000000000004: decoding with: DecoderValueSensor port: 8
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

            // 0000000000000004: message '{"value": 51}' sent to hub
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

            // Ensure device payload is available
            // Data: {"value": 51}
            var expectedPayload = $"{{\"value\":{msg}}}";
            await this.TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            this.TestFixtureCi.ClearLogs();

            msg = PayloadGenerator.Next().ToString();
            await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacketWithConfirmed: Expectation from serial
            // +CMSG: ACK Received
            await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

            // Checking than the communication occurs on DR 4 and RX2 as part of preferred windows RX2 and custom RX2 DR
            await AssertUtils.ContainsWithRetriesAsync(x => x.StartsWith("+CMSG: RXWIN2"), this.ArduinoDevice.SerialLogs);
            await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync(x => x.Contains($"\"datr\":\"SF9BW125\""), null);

            // 0000000000000004: decoding with: DecoderValueSensor port: 8
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

            // 0000000000000004: message '{"value": 51}' sent to hub
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

            // Ensure device payload is available
            // Data: {"value": 51}
            expectedPayload = $"{{\"value\":{msg}}}";
            await this.TestFixtureCi.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // rejoin
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);
            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);
            var joinSucceeded2 = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);
            Assert.True(joinSucceeded2, "Rejoin failed");

            if (device.IsMultiGw)
            {
                const string joinRefusedMsg = "join refused";
                var joinRefused = await this.TestFixtureCi.AssertNetworkServerModuleLogExistsAsync((s) => s.IndexOf(joinRefusedMsg) != -1, new SearchLogOptions(joinRefusedMsg));
                Assert.True(joinRefused.Found);
            }
        }
    }
}