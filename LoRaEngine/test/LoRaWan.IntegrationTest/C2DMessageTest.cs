using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace LoRaWan.IntegrationTest
{
    // Tests Cloud to Device messages
    [Collection("ArduinoSerialCollection")] // run in serial
    public sealed class C2DMessageTest : IClassFixture<IntegrationTestFixture>, IDisposable
    {
        private readonly IntegrationTestFixture testFixture;
        private LoRaArduinoSerial arduinoDevice;
        static Random random = new Random();

        public C2DMessageTest(IntegrationTestFixture testFixture)
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

        string ToHexString(string str)
        {
            var sb = new StringBuilder();

            var bytes = Encoding.UTF8.GetBytes(str);
            foreach (var t in bytes)
            {
                sb.Append(t.ToString("X2"));
            }

            return sb.ToString(); // returns: "48656C6C6F20776F726C64" for "Hello world"
        }

        // Ensures that C2D messages are received when working with confirmed messages
        // Uses Device9_OTAA
        [Fact]
        public async Task Test_OTAA_Confirmed_Receives_C2D_Message()
        {
            var device = this.testFixture.Device9_OTAA;
            Console.WriteLine($"Starting {nameof(Test_OTAA_Confirmed_Receives_C2D_Message)} using device {device.DeviceID}");      

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

            // Sends 2x confirmed messages
            for (var i=1; i <= 2; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                Console.WriteLine($"{device.DeviceID}: Sending confirmed '{msg}' {i}/10");

                await arduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.arduinoDevice.SerialLogs);

                this.arduinoDevice.ClearSerialLogs();
                testFixture.ClearNetworkServerModuleLog();
            }


            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            await this.testFixture.SendCloudToDeviceMessage(device.DeviceID, c2dMessageBody);
            Console.WriteLine($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var expectedRxSerial = $"+CMSG: PORT: 1; RX: \"{ToHexString(c2dMessageBody)}\"";
            Console.WriteLine($"Expected C2D received log is: {expectedRxSerial}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i=3; i <= 10; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                Console.WriteLine($"{device.DeviceID}: Sending confirmed '{msg}' {i}/10");
                await arduinoDevice.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(TimeSpan.FromSeconds(5));

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.arduinoDevice.SerialLogs);

                
                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var c2dLogMessage = $"{device.DeviceID}: C2D message: {c2dMessageBody}";
                var searchResults = await this.testFixture.SearchNetworkServerModuleAsync(
                    (messageBody) => {
                        return messageBody.StartsWith(c2dLogMessage);
                    },
                    new SearchLogOptions {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    Console.WriteLine($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to Device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;                    
                }
                
                
                var localFoundCloudToDeviceInSerial = this.arduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {                    
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }
                
                
                this.arduinoDevice.ClearSerialLogs();
                testFixture.ClearNetworkServerModuleLog();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (!foundReceivePacket)
                foundReceivePacket = this.arduinoDevice.SerialLogs.Contains(expectedRxSerial);
            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
                
        }


        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device10_OTAA
        [Fact]
        public async Task Test_OTAA_Unconfirmed_Receives_C2D_Message()
        {
            var device = this.testFixture.Device10_OTAA;
            Console.WriteLine($"Starting {nameof(Test_OTAA_Confirmed_Receives_C2D_Message)} using device {device.DeviceID}");      

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

            // Sends 2x confirmed messages
            for (var i=1; i <= 2; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                Console.WriteLine($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");

                await arduinoDevice.transferPacketAsync(msg, 10);                

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", arduinoDevice.SerialLogs);

                this.arduinoDevice.ClearSerialLogs();
                testFixture.ClearNetworkServerModuleLog();
            }


            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            await this.testFixture.SendCloudToDeviceMessage(device.DeviceID, c2dMessageBody);
            Console.WriteLine($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var expectedRxSerial = $"+MSG: PORT: 1; RX: \"{ToHexString(c2dMessageBody)}\"";
            Console.WriteLine($"Expected C2D received log is: {expectedRxSerial}");

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i=3; i <= 10; ++i)
            {
                var msg = PayloadGenerator.Next().ToString();
                Console.WriteLine($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                await arduinoDevice.transferPacketAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", arduinoDevice.SerialLogs);
                
                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                var c2dLogMessage = $"{device.DeviceID}: C2D message: {c2dMessageBody}";
                var searchResults = await this.testFixture.SearchNetworkServerModuleAsync(
                    (messageBody) => {
                        return messageBody.StartsWith(c2dLogMessage);
                    },
                    new SearchLogOptions {
                        Description = c2dLogMessage,
                        MaxAttempts = 1
                    });

                // We should only receive the message once
                if (searchResults.Found)
                {
                    Console.WriteLine($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to Device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;                    
                }
                
                
                var localFoundCloudToDeviceInSerial = this.arduinoDevice.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {                    
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }
                
                
                this.arduinoDevice.ClearSerialLogs();
                testFixture.ClearNetworkServerModuleLog();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (!foundReceivePacket)
                foundReceivePacket = this.arduinoDevice.SerialLogs.Contains(expectedRxSerial);
            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
                
        }
    }
}