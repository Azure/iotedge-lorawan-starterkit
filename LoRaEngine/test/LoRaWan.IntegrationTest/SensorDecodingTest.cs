

using System;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    // Tests sensor decoding test (http, reflection)
    [Collection("ArduinoSerialCollection")] // run in serial
    public class SensorDecodingTest: IntegrationTestBase
    {
        public SensorDecodingTest(IntegrationTestFixture testFixture) : base(testFixture)
        {
        }


        // Ensures that http sensor decoder decodes payload
        // Uses device Device11_OTAA
        [Fact]
        public async Task SensorDecoder_HttpBased_ValueSensorDecoder_DecodesPayload()
        {
            var device = this.TestFixture.Device11_OTAA;
            Log($"Starting {nameof(SensorDecoder_HttpBased_ValueSensorDecoder_DecodesPayload)} using device {device.DeviceID}");      

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixture.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            if (!joinSucceeded)
            {                
                Assert.True(joinSucceeded, "Join failed");
            }

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN); 

            
            await this.ArduinoDevice.transferPacketWithConfirmedAsync("1234", 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // +CMSG: ACK Received
            await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

            // Find "0000000000000011: message '{"value":1234}' sent to hub" in network server logs
            await this.TestFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":1234}}' sent to hub");

            this.ArduinoDevice.ClearSerialLogs();
            this.TestFixture.ClearNetworkServerModuleLog();
        }

        // Ensures that reflect based sensor decoder decodes payload
        // Uses Device12_OTAA
        [Fact]
        public async Task SensorDecoder_ReflectionBased_ValueSensorDecoder_DecodesPayload()
        {
            var device = this.TestFixture.Device12_OTAA;
            Log($"Starting {nameof(SensorDecoder_HttpBased_ValueSensorDecoder_DecodesPayload)} using device {device.DeviceID}");      

            await this.ArduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await this.ArduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await this.ArduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await this.ArduinoDevice.SetupLora(this.TestFixture.Configuration.LoraRegion);

            var joinSucceeded = await this.ArduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            if (!joinSucceeded)
            {                
                Assert.True(joinSucceeded, "Join failed");
            }

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN); 

            
            await this.ArduinoDevice.transferPacketWithConfirmedAsync("4321", 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // +CMSG: ACK Received
            await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

            // Find "0000000000000011: message '{"value":1234}' sent to hub" in network server logs
            await this.TestFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":4321}}' sent to hub");

            this.ArduinoDevice.ClearSerialLogs();
            this.TestFixture.ClearNetworkServerModuleLog();
        }
    }

}