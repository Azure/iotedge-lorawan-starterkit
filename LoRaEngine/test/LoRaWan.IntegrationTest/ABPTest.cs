// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.IntegrationTest
{
    using System;
    using System.Threading.Tasks;
    using LoRaWan.Test.Shared;
    using Xunit;

    // Tests ABP requests
    [Collection(Constants.TestCollectionName)] // run in serial
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class ABPTest : IntegrationTestBaseCi
    {
        public ABPTest(IntegrationTestFixtureCi testFixture)
            : base(testFixture)
        {
        }

        // Verifies that ABP confirmed and unconfirmed messages are working
        // Uses Device5_ABP
        [Fact]
        public async Task Test_ABP_Confirmed_And_Unconfirmed_Message()
        {
            const int MESSAGES_COUNT = 10;

            var device = this.TestFixtureCi.Device5_ABP;
            this.LogTestStart(device);
            await this.ArduinoDevice.setDeviceDefaultAsync();
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            // Sends 10x unconfirmed messages
            for (var i = 0; i < MESSAGES_COUNT; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i + 1}/{MESSAGES_COUNT}");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                this.TestFixtureCi.ClearLogs();
            }

            // Sends 10x confirmed messages
            for (var i = 0; i < MESSAGES_COUNT; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending confirmed '{msg}' {i + 1}/{MESSAGES_COUNT}");
                await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                this.TestFixtureCi.ClearLogs();
            }
        }

        // Verifies that ABP using wrong devAddr is ignored when sending messages
        // Uses Device6_ABP
        [Theory]
        [InlineData("05060708")]
        [InlineData("02060708")]
        public async Task Test_ABP_Wrong_DevAddr_Is_Ignored(string devAddrToUse)
        {
            var device = this.TestFixtureCi.Device6_ABP;
            this.LogTestStart(device);

            Assert.NotEqual(devAddrToUse, device.DevAddr);
            await this.ArduinoDevice.setDeviceDefaultAsync();
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(devAddrToUse, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            await this.ArduinoDevice.transferPacketAsync(PayloadGenerator.Next().ToString(), 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);
            if (devAddrToUse.StartsWith("02"))
            {
                // 02060708: device is not our device, ignore message
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                    $"{devAddrToUse}: device is not our device, ignore message",
                    $"{devAddrToUse}: device is not from our network, ignoring message");
            }
            else
            {
                // 05060708: device is using another network id, ignoring this message
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                 $"{devAddrToUse}: device is using another network id, ignoring this message");
            }

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            this.TestFixtureCi.ClearLogs();

            // Try with confirmed message
            await this.ArduinoDevice.transferPacketWithConfirmedAsync(PayloadGenerator.Next().ToString(), 10);

            // wait for serial logs to be ready
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacketWithConfirmed: Expectation from serial
            // +CMSG: ACK Received -- should not be there!
            Assert.DoesNotContain("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

            if (devAddrToUse.StartsWith("02"))
            {
                // 02060708: device is not our device, ignore message
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                    $"{devAddrToUse}: device is not our device, ignore message",
                    $"{devAddrToUse}: device is not from our network, ignoring message");
            }
            else
            {
                // 05060708: device is using another network id, ignoring this message
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync(
                 $"{devAddrToUse}: device is using another network id, ignoring this message");
            }
        }

        // Tests using a incorrect Network Session key, resulting device not ours
        // AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
        // NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
        // DevAddr="0028B1B2"
        // Uses Device7_ABP
        [Fact]
        public async Task Test_ABP_Mismatch_NwkSKey_And_AppSKey_Fails_Mic_Validation()
        {
            var device = this.TestFixtureCi.Device7_ABP;
            this.LogTestStart(device);

            var appSKeyToUse = "000102030405060708090A0B0C0D0E0F";
            var nwkSKeyToUse = "01020304050607080910111213141516";
            Assert.NotEqual(appSKeyToUse, device.AppSKey);
            Assert.NotEqual(nwkSKeyToUse, device.NwkSKey);
            await this.ArduinoDevice.setDeviceDefaultAsync();
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(nwkSKeyToUse, appSKeyToUse, null);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            await this.ArduinoDevice.transferPacketAsync(PayloadGenerator.Next().ToString(), 10);

            // wait for serial logs to be ready
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            // await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.lora.SerialLogs);

            // 0000000000000005: with devAddr 0028B1B0 check MIC failed. Device will be ignored from now on
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: with devAddr {device.DevAddr} check MIC failed");

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            this.TestFixtureCi.ClearLogs();

            // Try with confirmed message
            await this.ArduinoDevice.transferPacketWithConfirmedAsync(PayloadGenerator.Next().ToString(), 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // 0000000000000005: with devAddr 0028B1B0 check MIC failed
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: with devAddr {device.DevAddr} check MIC failed");

            // wait until arduino stops trying to send confirmed msg
            await this.ArduinoDevice.WaitForIdleAsync();
        }

        // Tests using a invalid Network Session key, resulting in mic failed
        // Uses Device8_ABP
        [Fact]
        public async Task Test_ABP_Invalid_NwkSKey_Fails_With_Mic_Error()
        {
            var device = this.TestFixtureCi.Device8_ABP;
            this.LogTestStart(device);

            var nwkSKeyToUse = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            Assert.NotEqual(nwkSKeyToUse, device.NwkSKey);
            await this.ArduinoDevice.setDeviceDefaultAsync();
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(nwkSKeyToUse, device.AppSKey, null);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            await this.ArduinoDevice.transferPacketAsync(PayloadGenerator.Next().ToString(), 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // After transferPacket: Expectation from serial
            // +MSG: Done
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

            // 0000000000000008: with devAddr 0028B1B3 check MIC failed. Device will be ignored from now on
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: with devAddr {device.DevAddr} check MIC failed");

            this.TestFixtureCi.ClearLogs();

            // Try with confirmed message
            await this.ArduinoDevice.transferPacketWithConfirmedAsync(PayloadGenerator.Next().ToString(), 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // 0000000000000008: with devAddr 0028B1B3 check MIC failed.
            await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DevAddr}: with devAddr {device.DevAddr} check MIC failed");

            // Before starting new test, wait until Lora drivers stops sending/receiving data
            await this.ArduinoDevice.WaitForIdleAsync();
        }

        // Verifies that ABP confirmed and unconfirmed messages are working
        // Uses Device16_ABP and Device17_ABP
        [Fact]
        public async Task Test_ABP_Device_With_Same_DevAddr()
        {
            const int MESSAGES_COUNT = 2;
            this.LogTestStart(new TestDeviceInfo[] { this.TestFixtureCi.Device16_ABP, this.TestFixtureCi.Device17_ABP });

            await this.SendABPMessages(MESSAGES_COUNT, this.TestFixtureCi.Device16_ABP);
            await this.SendABPMessages(MESSAGES_COUNT, this.TestFixtureCi.Device17_ABP);
        }

        private async Task SendABPMessages(int messages_count, TestDeviceInfo device)
        {
            await this.ArduinoDevice.setDeviceDefaultAsync();
            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await this.ArduinoDevice.SetupLora(this.TestFixtureCi.Configuration.LoraRegion);

            // Sends 10x unconfirmed messages
            for (var i = 0; i < messages_count; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending unconfirmed '{msg}' {i + 1}/{messages_count}");
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacket: Expectation from serial
                // +MSG: Done
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                this.TestFixtureCi.ClearLogs();
            }

            // Sends 10x confirmed messages
            for (var i = 0; i < messages_count; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                this.Log($"{device.DeviceID}: Sending confirmed '{msg}' {i + 1}/{messages_count}");
                await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.TestFixtureCi.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                this.TestFixtureCi.ClearLogs();
            }
        }
    }
}