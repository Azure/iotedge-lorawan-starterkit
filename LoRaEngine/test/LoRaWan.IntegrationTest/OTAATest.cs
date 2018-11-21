using System;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json.Linq;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    // Tests OTAA requests
    [Collection("ArduinoSerialCollection")] // run in serial
    public sealed class OTAATest : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        private readonly IntegrationTestFixture testFixture;
        private LoRaArduinoSerial arduinoDevice;

        public OTAATest(IntegrationTestFixture testFixture)
        {
            this.testFixture = testFixture;
            this.arduinoDevice = LoRaArduinoSerial.CreateFromPort(testFixture.Configuration.LeafDeviceSerialPort);
            this.testFixture.ClearNetworkServerModuleLog();
        }

        public void Dispose()
        {
            this.arduinoDevice?.Dispose();
            this.arduinoDevice = null;
            GC.SuppressFinalize(this);

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

            var device = this.testFixture.Device4_OTAA;
            Console.WriteLine($"Starting {nameof(Test_OTAA_Confirmed_And_Unconfirmed_Message)} using device {device.DeviceID}");      

            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await arduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion);

             var joinSucceeded = await arduinoDevice.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

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
                await arduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done                        
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.arduinoDevice.SerialLogs);


                // 0000000000000004: valid frame counter, msg: 1 server: 0
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000004: message '{"value": 51}' sent to hub
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await this.testFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                this.arduinoDevice.ClearSerialLogs();
                testFixture.ClearNetworkServerModuleLog();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            // Sends 10x confirmed messages
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                await arduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.arduinoDevice.SerialLogs);

                // 0000000000000004: decoding with: DecoderValueSensor port: 8
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000004: message '{"value": 51}' sent to hub
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                // Ensure device payload is available
                // Data: {"value": 51}
                var expectedPayload = $"{{\"value\":{msg}}}";
                await this.testFixture.AssertIoTHubDeviceMessageExistsAsync(device.DeviceID, expectedPayload);

                this.arduinoDevice.ClearSerialLogs();
                testFixture.ClearNetworkServerModuleLog();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }
        }
    }
}