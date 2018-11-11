using System;
using System.Linq;
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
                Console.WriteLine($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");

                lora.transferPacketWithConfirmed(msg, 10);

                await Task.Delay(TimeSpan.FromSeconds(5));

                //await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET * 20);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                Assert.Contains("+CMSG: ACK Received", this.lora.SerialLogs);

                await Task.Delay(Constants.DELAY_BETWEEN_MESSAGES);
            }


            this.lora.ClearSerialLogs();
            testFixture.ClearNetworkServerLogEvents();


            // sends C2D - between 10 and 99
            var c2dMessageBody = (100 + random.Next(90)).ToString();
            await this.testFixture.SendCloudToDeviceMessage(device.DeviceID, c2dMessageBody);
            Console.WriteLine($"Message {c2dMessageBody} sent to device, need to check if it receives");

            var foundC2DMessage = false;
            var foundReceivePacket = false;
            var expectedRxSerial = $"+CMSG: PORT: 1; RX: \"3{c2dMessageBody[0]}3{c2dMessageBody[1]}\"";

            // Sends 8x confirmed messages, stopping if C2D message is found
            for (var i=2; i <= 10; ++i)
            {
                var msg = (10 + i).ToString();
                Console.WriteLine($"{device.DeviceID}: Sending unconfirmed '{msg}' {i}/10");
                lora.transferPacketWithConfirmed(msg, 10);

                await Task.Delay(TimeSpan.FromSeconds(5));

                //await Task.Delay(Constants.DELAY_FOR_SERIAL_AFTER_SENDING_PACKET * 20);

                // After transferPacketWithConfirmed: Expectation from serial
                // +CMSG: ACK Received
                Assert.Contains("+CMSG: ACK Received", this.lora.SerialLogs);

                
                // check if c2d message was found
                // 0000000000000009: C2D message: 58
                (var foundExpectedLog, _) = await this.testFixture.FindNetworkServerEventLog((e, deviceID, messageBody) => {
                    return messageBody.StartsWith($"{device.DeviceID}: C2D message: {c2dMessageBody}");
                });

                foundC2DMessage = foundExpectedLog;                    
                Console.WriteLine($"{device.DeviceID}: Found C2D message in log (after sending {i}/10) ? {foundC2DMessage}");
                if (foundC2DMessage)
                    break;

                if (!foundReceivePacket)
                    foundReceivePacket = this.lora.SerialLogs.Contains(expectedRxSerial);
                
                
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