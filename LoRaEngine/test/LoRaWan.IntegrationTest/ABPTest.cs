using System;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    // Tests ABP requests
    [Collection("ArduinoSerialCollection")] // run in serial
    public sealed class ABPTest : IClassFixture<IntegrationTestFixture>, IDisposable
    {

        private readonly IntegrationTestFixture testFixture;
        private LoRaArduinoSerial arduinoDevice;

        public ABPTest(IntegrationTestFixture testFixture)
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


        // Verifies that ABP confirmed and unconfirmed messages are working
        // Uses Device5_ABP
        [Fact]
        public async Task Test_ABP_Confirmed_And_Unconfirmed_Message()
        {
            const int MESSAGES_COUNT = 10;

            var device = this.testFixture.Device5_ABP;
            Console.WriteLine($"Starting {nameof(Test_ABP_Confirmed_And_Unconfirmed_Message)} using device {device.DeviceID}");      

            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await arduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion); 

            // Sends 10x unconfirmed messages            
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                Console.WriteLine($"{device.DeviceID}: Sending unconfirmed '{msg}' {i+1}/{MESSAGES_COUNT}");
                await arduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacket: Expectation from serial
                // +MSG: Done                        
                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.arduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                this.arduinoDevice.ClearSerialLogs();
                testFixture.ClearNetworkServerModuleLog();
            }

            // Sends 10x confirmed messages
            for (var i=0; i < MESSAGES_COUNT; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                Console.WriteLine($"{device.DeviceID}: Sending confirmed '{msg}' {i+1}/{MESSAGES_COUNT}");
                await arduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.arduinoDevice.SerialLogs);

                // 0000000000000005: valid frame counter, msg: 1 server: 0
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: valid frame counter, msg:");

                // 0000000000000005: decoding with: DecoderValueSensor port: 8
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: decoding with: {device.SensorDecoder} port:");
            
                // 0000000000000005: message '{"value": 51}' sent to hub
                await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: message '{{\"value\":{msg}}}' sent to hub");

                this.arduinoDevice.ClearSerialLogs();
                testFixture.ClearNetworkServerModuleLog();
            }
        }

        // Verifies that ABP using wrong devAddr is ignored when sending messages
        // Uses Device6_ABP
        [Fact]
        public async Task Test_ABP_Wrong_DevAddr_Is_Ignored()
        {
            var device = this.testFixture.Device6_ABP;
            Console.WriteLine($"Starting {nameof(Test_ABP_Wrong_DevAddr_Is_Ignored)} using device {device.DeviceID}");      

            var devAddrToUse = "05060708";
            Assert.NotEqual(devAddrToUse, device.DevAddr);
            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await arduinoDevice.setIdAsync(devAddrToUse, device.DeviceID, null);
            await arduinoDevice.setKeyAsync(device.NwkSKey, device.AppSKey, null);

            await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion); 
            
            await arduinoDevice.transferPacketAsync(PayloadGenerator.Next().ToString(), 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacket: Expectation from serial
            // +MSG: Done                        
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.arduinoDevice.SerialLogs);

            // 05060708: device is not our device, ignore message
            await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{devAddrToUse}: device is not our device, ignore message");

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

             this.arduinoDevice.ClearSerialLogs();
            testFixture.ClearNetworkServerModuleLog();

            // Try with confirmed message
            await arduinoDevice.transferPacketWithConfirmedAsync(PayloadGenerator.Next().ToString(), 10);

            // wait for serial logs to be ready
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // After transferPacketWithConfirmed: Expectation from serial
            // +CMSG: ACK Received -- should not be there!
            Assert.DoesNotContain("+CMSG: ACK Received", this.arduinoDevice.SerialLogs);

            // 05060708: device is not our device, ignore message
            await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{devAddrToUse}: device is not our device, ignore message");

        }


        // Tests using a incorrect Network Session key, resulting device not ours
        // AppSKey="2B7E151628AED2A6ABF7158809CF4F3C",
        // NwkSKey="3B7E151628AED2A6ABF7158809CF4F3C",
        // DevAddr="0028B1B2"
        // Uses Device7_ABP
        [Fact]
        public async Task Test_ABP_Mismatch_NwkSKey_And_AppSKey_Fails_Mic_Validation()
        {
            var device = this.testFixture.Device7_ABP;
            Console.WriteLine($"Starting {nameof(Test_ABP_Mismatch_NwkSKey_And_AppSKey_Fails_Mic_Validation)} using device {device.DeviceID}");      

            var appSKeyToUse = "000102030405060708090A0B0C0D0E0F";
            var nwkSKeyToUse = "01020304050607080910111213141516";
            Assert.NotEqual(appSKeyToUse, device.AppSKey);
            Assert.NotEqual(nwkSKeyToUse, device.NwkSKey);
            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await arduinoDevice.setKeyAsync(nwkSKeyToUse, appSKeyToUse, null);

            await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion); 
            
            await arduinoDevice.transferPacketAsync(PayloadGenerator.Next().ToString(), 10);

            // wait for serial logs to be ready
            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);


            // After transferPacket: Expectation from serial
            // +MSG: Done                        
            //await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.lora.SerialLogs);
       
            // 0000000000000005: with devAddr 0028B1B0 check MIC failed. Device will be ignored from now on
            await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: with devAddr {device.DevAddr} check MIC failed. Device will be ignored from now on");

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            this.arduinoDevice.ClearSerialLogs();
            testFixture.ClearNetworkServerModuleLog();

            // Try with confirmed message

            await arduinoDevice.transferPacketWithConfirmedAsync(PayloadGenerator.Next().ToString(), 10);

            await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET);

            // 0000000000000005: with devAddr 0028B1B0 check MIC failed. Device will be ignored from now on
            await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: with devAddr {device.DevAddr} check MIC failed. Device will be ignored from now on");

            // wait until arduino stops trying to send confirmed msg
            await this.arduinoDevice.WaitForIdleAsync();
        }    

        // Tests using a invalid Network Session key, resulting in mic failed
        // Uses Device8_ABP
        [Fact]
        public async Task Test_ABP_Invalid_NwkSKey_Fails_With_Mic_Error()
        {
            var device = this.testFixture.Device8_ABP;
            Console.WriteLine($"Starting {nameof(Test_ABP_Invalid_NwkSKey_Fails_With_Mic_Error)} using device {device.DeviceID}");      

            var nwkSKeyToUse = "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
            Assert.NotEqual(nwkSKeyToUse, device.NwkSKey);
            await arduinoDevice.setDeviceModeAsync(LoRaArduinoSerial._device_mode_t.LWABP);
            await arduinoDevice.setIdAsync(device.DevAddr, device.DeviceID, null);
            await arduinoDevice.setKeyAsync(nwkSKeyToUse, device.AppSKey, null);

            await arduinoDevice.SetupLora(this.testFixture.Configuration.LoraRegion); 
            
            await arduinoDevice.transferPacketAsync(PayloadGenerator.Next().ToString(), 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // After transferPacket: Expectation from serial
            // +MSG: Done                        
            await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", this.arduinoDevice.SerialLogs);

            // 0000000000000008: with devAddr 0028B1B3 check MIC failed. Device will be ignored from now on
            await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: with devAddr {device.DevAddr} check MIC failed. Device will be ignored from now on");

    
            this.arduinoDevice.ClearSerialLogs();
            testFixture.ClearNetworkServerModuleLog();

            // Try with confirmed message

            await arduinoDevice.transferPacketWithConfirmedAsync(PayloadGenerator.Next().ToString(), 10);

            await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

            // 0000000000000008: with devAddr 0028B1B3 check MIC failed. Device will be ignored from now on
            await this.testFixture.AssertNetworkServerModuleLogStartsWithAsync($"{device.DeviceID}: with devAddr {device.DevAddr} check MIC failed. Device will be ignored from now on");


            // Before starting new test, wait until Lora drivers stops sending/receiving data
            await arduinoDevice.WaitForIdleAsync();
        }        
    }
}