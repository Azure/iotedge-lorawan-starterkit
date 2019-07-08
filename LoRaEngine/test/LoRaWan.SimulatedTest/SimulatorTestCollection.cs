// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace LoRaWan.SimulatedTest
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using LoRaTools;
    using LoRaTools.LoRaMessage;
    using LoRaTools.LoRaPhysical;
    using LoRaWan.Test.Shared;
    using Xunit;

    // Tests ABP requests
    [Trait("Category", "SkipWhenLiveUnitTesting")]
    public sealed class SimulatorTestCollection : IntegrationTestBaseSim
    {
        private readonly TimeSpan intervalBetweenMessages;
        private readonly TimeSpan intervalAfterJoin;
        private TestConfiguration configuration = TestConfiguration.GetConfiguration();

        public TestConfiguration Configuration { get => this.configuration; set => this.configuration = value; }

        public SimulatorTestCollection(IntegrationTestFixtureSim testFixture)
            : base(testFixture)
        {
            this.intervalBetweenMessages = TimeSpan.FromSeconds(5);
            this.intervalAfterJoin = TimeSpan.FromSeconds(10);
        }

        // check if we need to parametrize address
        // IPEndPoint CreateNetworkServerEndpoint() => new IPEndPoint(IPAddress.Broadcast, 1680);
        IPEndPoint CreateNetworkServerEndpoint() => new IPEndPoint(IPAddress.Parse(this.Configuration.NetworkServerIP), 1680);

        // [Fact]
        // public async Task Ten_Devices_Sending_Messages_Each_Second()
        // {
        //     var listSimulatedDevices = new List<SimulatedDevice>();
        //     foreach (var device in this.TestFixture.DeviceRange1000_ABP)
        //     {
        //         var simulatedDevice = new SimulatedDevice(device);
        //         listSimulatedDevices.Add(simulatedDevice);
        //     }

        // var networkServerIPEndpoint = CreateNetworkServerEndpoint();

        // using (var simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
        //     {
        //         simulatedPacketForwarder.Start();

        // var deviceTasks = new List<Task>();
        //         foreach (var device in this.TestFixture.DeviceRange1000_ABP)
        //         {
        //             var simulatedDevice = new SimulatedDevice(device);
        //             deviceTasks.Add(SendDeviceMessagesAsync(simulatedPacketForwarder, simulatedDevice, 60, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)));
        //             await Task.Delay(2000);
        //         }

        // await Task.WhenAll(deviceTasks);
        //         await simulatedPacketForwarder.StopAsync();
        //     }

        // var eventsByDevices = this.TestFixture.IoTHubMessages.GetEvents().GroupBy(x => x.SystemProperties["iothub-connection-device-id"]);
        //     Assert.Equal(10, eventsByDevices.Count());
        // }
        [Fact]
        public async Task Single_ABP_Simulated_Device()
        {
            const int MessageCount = 5;

            TestDeviceInfo device = this.TestFixtureSim.Device1001_Simulated_ABP;
            SimulatedDevice simulatedDevice = new SimulatedDevice(device);
            IPEndPoint networkServerIPEndpoint = this.CreateNetworkServerEndpoint();

            using (SimulatedPacketForwarder simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
            {
                simulatedPacketForwarder.Start();

                for (int i = 1; i <= MessageCount; i++)
                {
                    await simulatedDevice.SendUnconfirmedMessageAsync(simulatedPacketForwarder, i.ToString());
                    // await simulatedDevice.SendConfirmedMessageAsync(simulatedPacketForwarder, i.ToString());
                    await Task.Delay(this.intervalBetweenMessages);
                }

                await simulatedPacketForwarder.StopAsync();
            }
        }

        [Fact]
        public async Task Single_OTAA_Simulated_Device()
        {
            const int MessageCount = 5;

            TestDeviceInfo device = this.TestFixtureSim.Device1002_Simulated_OTAA;
            SimulatedDevice simulatedDevice = new SimulatedDevice(device);
            IPEndPoint networkServerIPEndpoint = this.CreateNetworkServerEndpoint();

            using (SimulatedPacketForwarder simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
            {
                simulatedPacketForwarder.Start();

                bool joined = await simulatedDevice.JoinAsync(simulatedPacketForwarder);
                Assert.True(joined, "OTAA join failed");

                await Task.Delay(this.intervalAfterJoin);

                for (int i = 1; i <= MessageCount; i++)
                {
                    await simulatedDevice.SendUnconfirmedMessageAsync(simulatedPacketForwarder, i.ToString());
                    await Task.Delay(this.intervalBetweenMessages);
                }

                await simulatedPacketForwarder.StopAsync();
            }

            // wait 10 seconds before checking if iot hub content is available
            await Task.Delay(TimeSpan.FromSeconds(10));

            IEnumerable<Microsoft.Azure.EventHubs.EventData> msgsFromDevice = this.TestFixture.IoTHubMessages.GetEvents().Where(x => x.GetDeviceId() == simulatedDevice.LoRaDevice.DeviceID);
            int actualAmountOfMsgs = msgsFromDevice.Where(x => !x.Properties.ContainsKey("iothub-message-schema")).Count();
            Assert.Equal(MessageCount, actualAmountOfMsgs);
        }

        [Fact(Skip = "simulated")]
        public async Task Simulated_Http_Based_Decoder_Scenario()
        {
            TestDeviceInfo device = this.TestFixtureSim.Device1003_Simulated_HttpBasedDecoder;
            SimulatedDevice simulatedDevice = new SimulatedDevice(device);
            IPEndPoint networkServerIPEndpoint = this.CreateNetworkServerEndpoint();

            using (SimulatedPacketForwarder simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
            {
                simulatedPacketForwarder.Start();

                bool joined = await simulatedDevice.JoinAsync(simulatedPacketForwarder);
                Assert.True(joined, "OTAA join failed");

                await Task.Delay(this.intervalAfterJoin);

                for (int i = 1; i <= 3; i++)
                {
                    await simulatedDevice.SendUnconfirmedMessageAsync(simulatedPacketForwarder, i.ToString());
                    await Task.Delay(this.intervalBetweenMessages);
                }

                await simulatedPacketForwarder.StopAsync();
            }

            // wait 10 seconds before checking if iot hub content is available
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        // Scenario:
        // - 100x ABP devices
        // - 10x OTAA devices
        // - Sending unconfirmed messages
        // - Goal: 20 devices in parallel
        [Fact]
        public async Task Multiple_ABP_and_OTAA_Simulated_Devices_Unconfirmed()
        {
            // amount of devices to test with. Maximum is 89
            // Do go beyond 100 deviceClients in IoT Edge, use edgeHub env var 'MaxConnectedClients'
            int scenarioDeviceNumber = 250; // Default: 20

            // amount of messages to send per device (without warm-up phase)
            int scenarioMessagesPerDevice = 10; // Default: 10

            // amount of devices to send data in parallel
            int scenarioDeviceStepSize = 20; // Default: 20

            // amount of devices to send data in parallel for the warm-up phase
            int warmUpDeviceStepSize = 2; // Default: 10

            // amount of messages to send before device join is to occur
            int messagesBeforeJoin = 10; // Default: 10

            // amount of Unconfirmed messges to send before Confirmed message is to occur
            int messagesBeforeConfirmed = 5; // Default: 5

            // amount of seconds to wait between sends in warmup phase
            int delayWarmup = 5 * 1000; // Default 5 * 1000

            // amount of seconds to wait between sends in main phase
            int delayMessageSending = 5 * 1000; // Default 5 * 1000

            // amount of miliseconds to wait before checking LoRaWanNetworkSrvModule
            // for successful sending of messages to IoT Hub.
            // delay for 100 devices: 2 * 60 * 1000
            // delay for 20 devices: 15 * 1000
            int delayNetworkServerCheck = 15 * 60 * 1000;

            // amount of miliseconds to wait before checking of messages in IoT Hub
            // delay for 100 devices: 1 * 60 * 1000
            // delay for 20 devices: 15 * 1000
            int delayIoTHubCheck = 5 * 60 * 1000;

            // Get random number seed
            Random rnd = new Random();
            int seed = rnd.Next(100, 999);

            int count = 0;
            List<SimulatedDevice> listSimulatedDevices = new List<SimulatedDevice>();
            foreach (TestDeviceInfo device in this.TestFixtureSim.DeviceRange2000_1000_ABP)
            {
                if (count < scenarioDeviceNumber)
                {
                    SimulatedDevice simulatedDevice = new SimulatedDevice(device);
                    listSimulatedDevices.Add(simulatedDevice);
                    count++;
                }
            }

            int totalDevices = listSimulatedDevices.Count;
            int totalJoinDevices = 0;
            int totalJoins = 0;

            List<SimulatedDevice> listSimulatedJoinDevices = new List<SimulatedDevice>();
            foreach (TestDeviceInfo joinDevice in this.TestFixtureSim.DeviceRange3000_10_OTAA)
            {
                SimulatedDevice simulatedJoinDevice = new SimulatedDevice(joinDevice);
                listSimulatedJoinDevices.Add(simulatedJoinDevice);
            }

            IPEndPoint networkServerIPEndpoint = this.CreateNetworkServerEndpoint();

            using (SimulatedPacketForwarder simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
            {
                simulatedPacketForwarder.Start();

                // 1. picking 2x devices send an initial message (warm device cache in NtwSrv module)
                //    timeout of 2 seconds between each loop
                List<Task> tasks = new List<Task>();
                for (int i = 0; i < totalDevices;)
                {
                    tasks.Clear();
                    foreach (SimulatedDevice device in listSimulatedDevices.Skip(i).Take(warmUpDeviceStepSize))
                    {
                        await Task.Delay(rnd.Next(10, 250)); // Sleep between 10 and 250ms

                        TestLogger.Log($"[WARM-UP] {device.LoRaDevice.DeviceID}");
                        tasks.Add(device.SendUnconfirmedMessageAsync(simulatedPacketForwarder, seed + "000"));
                    }

                    await Task.WhenAll(tasks);
                    await Task.Delay(delayWarmup);

                    i += warmUpDeviceStepSize;
                }

                // 2. picking 20x devices sends messages (send 10 messages for each device)
                //    timeout of 5 seconds between each
                int messageCounter = 1;
                int joinDevice = 0;

                for (int messageId = 1; messageId <= scenarioMessagesPerDevice; ++messageId)
                {
                    for (int i = 0; i < totalDevices;)
                    {
                        tasks.Clear();
                        string payload = seed + messageId.ToString().PadLeft(3, '0');

                        foreach (SimulatedDevice device in listSimulatedDevices.Skip(i).Take(scenarioDeviceStepSize))
                        {
                            await Task.Delay(rnd.Next(10, 250)); // Sleep between 10 and 250ms

                            if (messageCounter % messagesBeforeConfirmed != 0)
                            {
                                // send Unconfirmed message
                                tasks.Add(device.SendUnconfirmedMessageAsync(simulatedPacketForwarder, payload));
                            }
                            else
                            {
                                // send Confirmed message, not waiting for confirmation
                                tasks.Add(device.SendConfirmedMessageAsync(simulatedPacketForwarder, payload));
                            }

                            if (messageCounter % messagesBeforeJoin == 0)
                            {
                                await Task.Delay(rnd.Next(10, 250)); // Sleep between 10 and 250ms

                                // Send Join Request with waiting
                                // tasks.Add(listSimulatedJoinDevices[joinDevice].JoinAsync(simulatedPacketForwarder));

                                // Send Join Request without waiting
                                _ = listSimulatedJoinDevices[joinDevice].JoinAsync(simulatedPacketForwarder);
                                totalJoins++;

                                TestLogger.Log($"[INFO] Join request sent for {listSimulatedJoinDevices[joinDevice].LoRaDevice.DeviceID}");

                                joinDevice = (joinDevice == 9) ? 0 : joinDevice + 1;
                                totalJoinDevices++; // Number corrected below.
                            }

                            messageCounter++;
                        }

                        await Task.WhenAll(tasks);
                        await Task.Delay(delayMessageSending);

                        i += scenarioDeviceStepSize;
                    }
                }

                await simulatedPacketForwarder.StopAsync();
            }

            // Correct total number of JoinDevices back to a max. of 10
            totalJoinDevices = (totalJoinDevices > 10) ? 10 : totalJoinDevices;

            // Wait before executing to allow for all messages to be sent
            TestLogger.Log($"[INFO] Waiting for {delayNetworkServerCheck / 1000} sec. before the test continues...");
            await Task.Delay(delayNetworkServerCheck);

            // 3. test Network Server logs if messages have been sent successfully
            string expectedPayload;
            foreach (SimulatedDevice device in listSimulatedDevices)
            {
                TestLogger.Log($"[INFO] Looking for upstream messages for {device.LoRaDevice.DeviceID}");
                for (int messageId = 0; messageId <= scenarioMessagesPerDevice; ++messageId)
                {
                    // Find "<all Device ID>: message '{"value":<seed+0 to number of msg/device>}' sent to hub" in network server logs
                    expectedPayload = $"{device.LoRaDevice.DeviceID}: message '{{\"value\":{seed + messageId.ToString().PadLeft(3, '0')}}}' sent to hub";
                    await this.TestFixture.AssertNetworkServerModuleLogStartsWithAsync(expectedPayload);
                }
            }

            TestLogger.Log($"[INFO] Waiting for {delayIoTHubCheck / 1000} sec. before the test continues...");
            await Task.Delay(delayIoTHubCheck);

            // IoT Hub test for arrival of messages.
            Dictionary<object, List<Microsoft.Azure.EventHubs.EventData>> eventsByDevices = this.TestFixture.IoTHubMessages.GetEvents()
                .GroupBy(x => x.SystemProperties["iothub-connection-device-id"])
                .ToDictionary(x => x.Key, x => x.ToList());

            // 4. Check that we have the right amount of devices receiving messages in IoT Hub
            // Assert.Equal(totalDevices, eventsByDevices.Count());
            if (totalDevices == eventsByDevices.Count())
            {
                TestLogger.Log($"[INFO] Devices sending messages: {totalDevices + totalJoinDevices}, == Devices receiving messages in IoT Hub: {eventsByDevices.Count()}");
            }
            else
            {
                TestLogger.Log($"[WARN] Devices sending messages: {totalDevices + totalJoinDevices}, != Devices receiving messages in IoT Hub: {eventsByDevices.Count()}");
            }

            // 5. Check that the correct number of messages have arrived in IoT Hub per device
            //    Warn only.
            foreach (SimulatedDevice device in listSimulatedDevices)
            {
                // Assert.True(
                //    eventsByDevices.TryGetValue(device.LoRaDevice.DeviceID, out var events),
                //    $"No messages were found for device {device.LoRaDevice.DeviceID}");
                // if (events.Count > 0)
                if (eventsByDevices.TryGetValue(device.LoRaDevice.DeviceID, out List<Microsoft.Azure.EventHubs.EventData> events))
                {
                    int actualAmountOfMsgs = events.Where(x => !x.Properties.ContainsKey("iothub-message-schema")).Count();
                    // Assert.Equal((1 + scenarioMessagesPerDevice), actualAmountOfMsgs);
                    if ((1 + scenarioMessagesPerDevice) != actualAmountOfMsgs)
                    {
                        TestLogger.Log($"[WARN] Wrong events for device {device.LoRaDevice.DeviceID}. Actual: {actualAmountOfMsgs}. Expected {1 + scenarioMessagesPerDevice}");
                    }
                    else
                    {
                        TestLogger.Log($"[INFO] Correct events for device {device.LoRaDevice.DeviceID}. Actual: {actualAmountOfMsgs}. Expected {1 + scenarioMessagesPerDevice}");
                    }
                }
                else
                {
                    TestLogger.Log($"[WARN] No messages were found for device {device.LoRaDevice.DeviceID}");
                }
            }

            // 6. Check if all expected messages have arrived in IoT Hub
            //    Warn only.
            foreach (SimulatedDevice device in listSimulatedDevices)
            {
                TestLogger.Log($"[INFO] Looking for IoT Hub messages for {device.LoRaDevice.DeviceID}");
                for (int messageId = 0; messageId <= scenarioMessagesPerDevice; ++messageId)
                {
                    // Find message containing '{"value":<seed>.<0 to number of msg/device>}' for all leaf devices in IoT Hub
                    expectedPayload = $"{{\"value\":{seed + messageId.ToString().PadLeft(3, '0')}}}";
                    await this.TestFixture.AssertIoTHubDeviceMessageExistsAsync(device.LoRaDevice.DeviceID, expectedPayload);
                }
            }
        }
    }
}