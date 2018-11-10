using System;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    // Tests ABP requests
    public sealed class ABPTest : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        const int TIME_BETWEEN_MESSAGES = 1000 * 5;
        const int DELAY_AFTER_SENDING_PACKET = 100; 

        private readonly IntegrationTestFixture testFixture;
        private readonly LoraRegion loraRegion;
        private LoRaArduinoSerial lora;

        public ABPTest(IntegrationTestFixture testFixture)
        {
            this.testFixture = testFixture;
            this.loraRegion = LoraRegion.EU;
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
        public async Task Test_ABP_Confirmed_And_Unconfirmed_Message()
        {
            const int MESSAGES_COUNT = 10;

            var device = this.testFixture.Device5_ABP;
            Console.WriteLine($"Starting {nameof(Test_ABP_Confirmed_And_Unconfirmed_Message)} using device {device.DeviceID}");      

            await lora.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await lora.setIdAsync(device.DevAddr, device.DeviceID, null);
            await lora.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await lora.SetupLora(LoraRegion.EU); 

            // Sends 10x unconfirmed messages            
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = (100 + i).ToString();
                lora.transferPacket(msg, 10);

                // wait for serial logs to be ready
                await Task.Delay(DELAY_AFTER_SENDING_PACKET);


                // After transferPacket: Expectation from serial
                // +MSG: Done                        
                Assert.Contains("+MSG: Done", this.lora.SerialLogs);


                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000005: sending message {"time":null,"tmms":0,"tmst":188399595,"freq":868.5,"chan":2,"rfch":1,"stat":1,"modu":"LORA","datr":"SF7BW125","codr":"4/5","rssi":-59,"lsnr":7.5,"size":16,"data":{"value":100},"port":8,"fcnt":1,"eui":"0000000000000005","gatewayid":"itestArm1","edgets":1541886640047} to hub
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: sending message ");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: message '{{\"value\": {msg}}}' sent to hub");

                this.lora.ClearSerialLogs();
                testFixture.ClearNetworkServerLogEvents();

                await Task.Delay(TIME_BETWEEN_MESSAGES);
            }

            // Sends 10x confirmed messages
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = (50 + i).ToString();
                lora.transferPacketWithConfirmed(msg, 10);

                // wait for serial logs to be ready
                await Task.Delay(DELAY_AFTER_SENDING_PACKET);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                Assert.Contains("+CMSG: ACK Received", this.lora.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000005: sending message {"time":null,"tmms":0,"tmst":188399595,"freq":868.5,"chan":2,"rfch":1,"stat":1,"modu":"LORA","datr":"SF7BW125","codr":"4/5","rssi":-59,"lsnr":7.5,"size":16,"data":{"value":100},"port":8,"fcnt":1,"eui":"0000000000000005","gatewayid":"itestArm1","edgets":1541886640047} to hub
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: sending message ");

                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: message '{{\"value\": {msg}}}' sent to hub");

                this.lora.ClearSerialLogs();
                testFixture.ClearNetworkServerLogEvents();

                await Task.Delay(TIME_BETWEEN_MESSAGES);
            }
        }

        [Fact]
        public async Task Test_ABP_Wrong_DevAddr_Fails_Sending_Confirmed_And_Unconfirmed()
        {
            var device = this.testFixture.Device6_ABP;
            Console.WriteLine($"Starting {nameof(Test_ABP_Wrong_DevAddr_Fails_Sending_Confirmed_And_Unconfirmed)} using device {device.DeviceID}");      

            await lora.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await lora.setIdAsync(device.DevAddr, device.DeviceID, null);
            await lora.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await lora.SetupLora(LoraRegion.EU); 
            
            lora.transferPacket("100", 10);

            // wait for serial logs to be ready
            await Task.Delay(DELAY_AFTER_SENDING_PACKET);


            // After transferPacket: Expectation from serial
            // +MSG: Done                        
            Assert.Contains("+MSG: Done", this.lora.SerialLogs);


            // 0000000000000005: valid frame counter, msg: 1 server: 0
            await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: valid frame counter, msg:");

            // 0000000000000005: decoding with: DecoderValueSensor port: 8
            await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
        
            // 0000000000000005: sending message {"time":null,"tmms":0,"tmst":188399595,"freq":868.5,"chan":2,"rfch":1,"stat":1,"modu":"LORA","datr":"SF7BW125","codr":"4/5","rssi":-59,"lsnr":7.5,"size":16,"data":{"value":100},"port":8,"fcnt":1,"eui":"0000000000000005","gatewayid":"itestArm1","edgets":1541886640047} to hub
            await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: sending message ");

            // 0000000000000005: message '{"value": 51}' sent to hub
            await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: message '{{\"value\": 100}}' sent to hub");

            this.lora.ClearSerialLogs();
            testFixture.ClearNetworkServerLogEvents();

            await Task.Delay(TIME_BETWEEN_MESSAGES);
        }

        [Fact]
        public async Task Test_ABP_Wrong_NwkSKey_Fails_Sending_Confirmed_And_Unconfirmed()
        {
            var device = this.testFixture.Device7_ABP;
            Console.WriteLine($"Starting {nameof(Test_ABP_Wrong_NwkSKey_Fails_Sending_Confirmed_And_Unconfirmed)} using device {device.DeviceID}");      

            var nwkSKeyToUse = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            Assert.NotEqual(nwkSKeyToUse, device.NwkSKey);
            await lora.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await lora.setIdAsync(device.DevAddr, device.DeviceID, null);
            await lora.setKeyAsync(nwkSKeyToUse, device.AppSKey, null);

            await lora.SetupLora(LoraRegion.EU); 
            
            lora.transferPacket("100", 10);

            // wait for serial logs to be ready
            await Task.Delay(DELAY_AFTER_SENDING_PACKET);


            // After transferPacket: Expectation from serial
            // +MSG: Done                        
            Assert.Contains("+MSG: Done", this.lora.SerialLogs);


            // 0000000000000005: valid frame counter, msg: 1 server: 0
            await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: valid frame counter, msg:");

            // 0000000000000005: decoding with: DecoderValueSensor port: 8
            await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
        
            // 0000000000000005: sending message {"time":null,"tmms":0,"tmst":188399595,"freq":868.5,"chan":2,"rfch":1,"stat":1,"modu":"LORA","datr":"SF7BW125","codr":"4/5","rssi":-59,"lsnr":7.5,"size":16,"data":{"value":100},"port":8,"fcnt":1,"eui":"0000000000000005","gatewayid":"itestArm1","edgets":1541886640047} to hub
            await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: sending message ");

            // 0000000000000005: message '{"value": 51}' sent to hub
            await this.testFixture.ValidateNetworkServerEventLog($"{device.DeviceID}: message '{{\"value\": 100}}' sent to hub");

            this.lora.ClearSerialLogs();
            testFixture.ClearNetworkServerLogEvents();

            await Task.Delay(TIME_BETWEEN_MESSAGES);
        }        
    }
}