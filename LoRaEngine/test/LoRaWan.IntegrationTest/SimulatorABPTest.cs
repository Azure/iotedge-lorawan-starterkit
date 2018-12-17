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
using Xunit;

namespace LoRaWan.IntegrationTest
{
    // Tests ABP requests
    [Collection(Constants.TestCollectionName)] // run in serial
    public sealed class SimulatorABPTest : IntegrationTestBase
    {
        private readonly TimeSpan intervalBetweenMessages;
        private readonly TimeSpan intervalAfterJoin;

        public SimulatorABPTest(IntegrationTestFixture testFixture) : base(testFixture)
        {
            this.intervalBetweenMessages = TimeSpan.FromSeconds(5);
            this.intervalAfterJoin = TimeSpan.FromSeconds(10);

        }

        // check if we need to parametrize address
        IPEndPoint CreateNetworkServerEndpoint() => new IPEndPoint(IPAddress.Broadcast, 1680);
      

        
        //[Fact]
        // public async Task Ten_Devices_Sending_Messages_Each_Second()
        // {
        //     var listSimulatedDevices = new List<SimulatedDevice>();
        //     foreach (var device in this.TestFixture.DeviceRange1000_ABP)
        //     {
        //         var simulatedDevice = new SimulatedDevice(device);
        //         listSimulatedDevices.Add(simulatedDevice);
        //     }
            
        //     var networkServerIPEndpoint = CreateNetworkServerEndpoint();

        //     using (var simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
        //     {
        //         simulatedPacketForwarder.Start();

        //         var deviceTasks = new List<Task>();
        //         foreach (var device in this.TestFixture.DeviceRange1000_ABP)
        //         {
        //             var simulatedDevice = new SimulatedDevice(device);
        //             deviceTasks.Add(SendDeviceMessagesAsync(simulatedPacketForwarder, simulatedDevice, 60, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1)));
        //             await Task.Delay(2000);
        //         }

        //         await Task.WhenAll(deviceTasks);
        //         await simulatedPacketForwarder.StopAsync();
        //     }

        //     var eventsByDevices = this.TestFixture.IoTHubMessages.GetEvents().GroupBy(x => x.SystemProperties["iothub-connection-device-id"]);
        //     Assert.Equal(10, eventsByDevices.Count());
        // }
        

        //[Fact]
        public async Task Single_ABP_Simulated_Device()
        {
            const int MessageCount = 5;

            var device = this.TestFixture.Device18_ABP;
            var simulatedDevice = new SimulatedDevice(device);
            var networkServerIPEndpoint = CreateNetworkServerEndpoint();

            using (var simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
            {
                simulatedPacketForwarder.Start();

                for (var i=1; i <= MessageCount; i++)
                {
                    await simulatedDevice.SendUnconfirmedMessageAsync(simulatedPacketForwarder, i.ToString());
                    await Task.Delay(intervalBetweenMessages);
                }

                await simulatedPacketForwarder.StopAsync();
            }
        }

        //[Fact]
        public async Task Single_OTAA_Simulated_Device()
        {
            const int MessageCount = 5;

            var device = this.TestFixture.Device19_OTAA;
            var simulatedDevice = new SimulatedDevice(device);
            var networkServerIPEndpoint = CreateNetworkServerEndpoint();

            using (var simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
            {
                simulatedPacketForwarder.Start();

                bool joined = await simulatedDevice.JoinAsync(simulatedPacketForwarder);
                Assert.True(joined, "OTAA join failed");

                await Task.Delay(intervalAfterJoin);

                for (var i=1; i <= MessageCount; i++)
                {
                    await simulatedDevice.SendUnconfirmedMessageAsync(simulatedPacketForwarder, i.ToString());
                    await Task.Delay(intervalBetweenMessages);
                }

                await simulatedPacketForwarder.StopAsync();
            }

            // wait 10 seconds before checking if iot hub content is available
            await Task.Delay(TimeSpan.FromSeconds(10));

            var msgsFromDevice = this.TestFixture.IoTHubMessages.GetEvents().Where(x => x.GetDeviceId() == simulatedDevice.LoRaDevice.DeviceID);
            Assert.Equal(MessageCount, msgsFromDevice.Count());
        }


        //[Fact]
        public async Task Simulated_Http_Based_Decoder_Scenario()
        {
            var device = this.TestFixture.Device20_Simulated_HttpBasedDecoder;
            var simulatedDevice = new SimulatedDevice(device);
            var networkServerIPEndpoint = CreateNetworkServerEndpoint();

            using (var simulatedPacketForwarder = new SimulatedPacketForwarder(networkServerIPEndpoint))
            {
                simulatedPacketForwarder.Start();

                bool joined = await simulatedDevice.JoinAsync(simulatedPacketForwarder);
                Assert.True(joined, "OTAA join failed");

                await Task.Delay(intervalAfterJoin);

                for (var i=1; i <= 3; i++)
                {
                    await simulatedDevice.SendUnconfirmedMessageAsync(simulatedPacketForwarder, i.ToString());
                    await Task.Delay(intervalBetweenMessages);
                }

                await simulatedPacketForwarder.StopAsync();
            }

            // wait 10 seconds before checking if iot hub content is available
            await Task.Delay(TimeSpan.FromSeconds(10));

        }
    }
}