using System;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    // Tests OTAA requests
    [Collection("ArduinoSerialCollection")] // run in serial
    public sealed class OTAATest : IntegrationTestBase
    {

        public OTAATest(IntegrationTestFixture testFixture) : base(testFixture)
        {
        }

        // Performs a OTAA join and sends N confirmed and unconfirmed messages
        // Expects that:
        // - device message is available on IoT Hub
        // - frame counter validation is done
        // - Message is decoded 
        [Fact]
        public async Task Test_OTAA_Confirmed_And_Unconfirmed_Message()
        {
            const int MESSAGES_COUNT = 10;

            var device = this.TestFixture.Device4_OTAA;
            Log($"Starting {nameof(Test_OTAA_Confirmed_And_Unconfirmed_Message)} using device {device.DeviceID}");      

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

            // Sends 10x unconfirmed messages            
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                await this.ArduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done                        
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.ArduinoDevice.SerialLogs);


                // 0000000000000004: valid frame counter, msg: 1 server: 0
                await this.TestFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await this.TestFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000004: message '{"value": 51}' sent to hub
                await this.TestFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await this.TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                this.ArduinoDevice.ClearSerialLogs();
                this.TestFixture.ClearNetworkServerModuleLog();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            // Sends 10x confirmed messages
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                await this.ArduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.ArduinoDevice.SerialLogs);

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await this.TestFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000004: message '{"value": 51}' sent to hub
                await this.TestFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await this.TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                this.ArduinoDevice.ClearSerialLogs();
                this.TestFixture.ClearNetworkServerModuleLog();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }
        }
    }
}