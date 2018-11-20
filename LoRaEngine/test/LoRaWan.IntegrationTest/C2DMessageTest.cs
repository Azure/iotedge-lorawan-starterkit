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
        private LoRaArduinoSerial lora;
        static Random random = new Random();

        public C2DMessageTest(IntegrationTestFixture testFixture)
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

            // Sends 2x confirmed messages
            for (var i=1; i <= 2; ++i)
            {
                var msg = (10 + i).ToString();
                Console.WriteLine($"{device.DeviceID}: Sending confirmed '{msg}' {i}/10");

                await lora.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.lora.SerialLogs);

                this.lora.ClearSerialLogs();
                testFixture.ClearNetworkServerLogEvents();
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
            for (var i=2; i <= 10; ++i)
            {
                var msg = (10 + i).ToString();
                Console.WriteLine($"{device.DeviceID}: Sending confirmed '{msg}' {i}/10");
                await lora.transferPacketWithConfirmedAsync(msg, 10);

                await Task.Delay(TimeSpan.FromSeconds(5));

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                await AssertUtils.ContainsWithRetriesAsync("+CMSG: ACK Received", this.lora.SerialLogs);

                
                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                (var foundC2DMessageInNetworkServerLog, _) = await this.testFixture.FindNetworkServerEventLog((e, deviceID, messageBody) => {
                    return messageBody.StartsWith($"{device.DeviceID}: C2D message: {c2dMessageBody}");
                },
                new FindNetworkServerEventLogOptions {
                    Description = $"{device.DeviceID}: C2D message: {c2dMessageBody}",
                    MaxAttempts = 1
                });

                // We should only receive the message once
                if (foundC2DMessageInNetworkServerLog)
                {
                    Console.WriteLine($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to Device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;                    
                }
                
                
                var localFoundCloudToDeviceInSerial = this.lora.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {                    
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }
                
                
                this.lora.ClearSerialLogs();
                testFixture.ClearNetworkServerLogEvents();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (!foundReceivePacket)
                foundReceivePacket = this.lora.SerialLogs.Contains(expectedRxSerial);
            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
                
        }


        // Ensures that C2D messages are received when working with unconfirmed messages
        // Uses Device10_OTAA
        [Fact]
        public async Task Test_OTAA_Unconfirmed_Receives_C2D_Message()
        {
            var device = this.testFixture.Device10_OTAA;
            Console.WriteLine($"Starting {nameof(Test_OTAA_Confirmed_Receives_C2D_Message)} using device {device.DeviceID}");      

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

            // Sends 2x confirmed messages
            for (var i=1; i <= 2; ++i)
            {
                var msg = (10 + i).ToString();
                Console.WriteLine($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");

                lora.transferPacket(msg, 10);                

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", lora.SerialLogs);

                this.lora.ClearSerialLogs();
                testFixture.ClearNetworkServerLogEvents();
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
            for (var i=2; i <= 10; ++i)
            {
                var msg = (10 + i).ToString();
                Console.WriteLine($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                lora.transferPacket(msg, 10);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);

                await AssertUtils.ContainsWithRetriesAsync("+MSG: Done", lora.SerialLogs);
                
                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                (var foundC2DMessageInNetworkServerLog, _) = await this.testFixture.FindNetworkServerEventLog((e, deviceID, messageBody) => {
                    return messageBody.StartsWith($"{device.DeviceID}: C2D message: {c2dMessageBody}");
                },
                new FindNetworkServerEventLogOptions {
                    Description = $"{device.DeviceID}: C2D message: {c2dMessageBody}",
                    MaxAttempts = 1
                });

                // We should only receive the message once
                if (foundC2DMessageInNetworkServerLog)
                {
                    Console.WriteLine($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                    Assert.False(foundC2DMessage, "Cloud to Device message should have been detected in Network Service module only once");
                    foundC2DMessage = true;                    
                }
                
                
                var localFoundCloudToDeviceInSerial = this.lora.SerialLogs.Contains(expectedRxSerial);
                if (localFoundCloudToDeviceInSerial)
                {                    
                    Assert.False(foundReceivePacket, "Cloud to device message should have been received only once");
                    foundReceivePacket = true;
                }
                
                
                this.lora.ClearSerialLogs();
                testFixture.ClearNetworkServerLogEvents();

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }

            Assert.True(foundC2DMessage, $"Did not find '{device.DeviceID}: C2D message: {c2dMessageBody}' in logs");

            // checks if log arrived
            if (!foundReceivePacket)
                foundReceivePacket = this.lora.SerialLogs.Contains(expectedRxSerial);
            Assert.True(foundReceivePacket, $"Could not find lora receiving message '{expectedRxSerial}'");
                
        }
    }
}