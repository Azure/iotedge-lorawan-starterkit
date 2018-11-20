using System;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    // Tests OTAA requests
    [Collection("ArduinoSerialCollection")] // run in serial
    public sealed class OTAATest : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        private readonly IntegrationTestFixture testFixture;
        private LoRaArduinoSerial lora;

        public OTAATest(IntegrationTestFixture testFixture)
        {
            this.testFixture = testFixture;
            this.lora = LoRaArduinoSerial.CreateFromPort(testFixture.Configuration.LeafDeviceSerialPort);
            this.testFixture.ClearNetworkServerLogEvents();
        }

        public void Dispose()
        {
            this.lora?.Dispose();
            this.lora = null;
            GC.SuppressFinalize(this);

        }

        [Fact]
        public async Task Test_OTAA_Confirmed_And_Unconfirmed_Message()
        {
            const int MESSAGES_COUNT = 10;

            var device = this.testFixture.Device4_OTAA;
            Console.WriteLine($"Starting {nameof(Test_OTAA_Confirmed_And_Unconfirmed_Message)} using device {device.DeviceID}");      

            await lora.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWOTAA);
            await lora.setIdAsync(device.DevAddr, device.DeviceID, device.AppEUI);
            await lora.setKeyAsync(device.NwkSKey, device.AppSKey, device.AppKey);

            await lora.SetupLora(this.testFixture.Configuration.LoraRegion);

             var joinSucceeded = await lora.setOTAAJoinAsyncWithRetry(LoRaArduinoSerial._otaa_join_cmd_t.JOIN, 20000, 5);

            if (!joinSucceeded)
            {                
                Assert.True(joinSucceeded, "Join failed");
            }

            // wait 1 second after joined
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_JOIN); 

            // Sends 10x unconfirmed messages            
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = (100 + i).ToString();
                await lora.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacket: Expectation from serial
                // +MSG: Done                        
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.lora.SerialLogs);


                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.testFixture.ValidateNetworkServerEventLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.testFixture.ValidateNetworkServerEventLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.testFixture.ValidateNetworkServerEventLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                this.lora.ClearSerialLogs();
                testFixture.ClearNetworkServerLogEvents();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            // Sends 10x confirmed messages
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = (50 + i).ToString();
                await lora.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.lora.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                //await this.testFixture.ValidateNetworkServerEventLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.testFixture.ValidateNetworkServerEventLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.testFixture.ValidateNetworkServerEventLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                this.lora.ClearSerialLogs();
                testFixture.ClearNetworkServerLogEvents();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }
        }
    }
}