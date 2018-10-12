using LoRaWan;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Diagnostics.CodeAnalysis;
using LoRaTools;

namespace LoRaSimulator
{
    public class Simulator : IDisposable
    {
        const int PORT = 1681;

        static UdpClient udpClient;

        private static IPAddress remoteLoRaAggregatorIp = IPAddress.Broadcast;
        private static int remoteLoRaAggregatorPort = 1680;

        private List<SimulatedDevice> listDevices = new List<SimulatedDevice>();
        private GatewayDevice gateway;

        private static byte[] mac;

        [SuppressMessage("Await.Warning", "CS4014:Await.Warning")]
        public async Task RunServer()
        {
            mac = GetMacAddress();
            Logger.Log("Starting LoRaWAN Simulator...", Logger.LoggingLevel.Always);

            // Creating the endpoint
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            udpClient = new UdpClient(endPoint);

            Logger.Log($"LoRaWAN Simulator started on port {PORT}", Logger.LoggingLevel.Always);
            //send first sync
            await UdpSendMessage(GetSyncHeader(PhysicalIdentifier.PULL_DATA));


            // Reading the test configuration
            string json = System.IO.File.ReadAllText(@".\testconfig.json");

            var theObjects = JsonConvert.DeserializeObject<JObject>(json);

            var rxpk = theObjects["rxpk"];
            gateway = new GatewayDevice(rxpk.ToString());

            var devices = theObjects["Devices"];

            // TODO: Need to create simulated devices
            foreach (var device in devices)
            {
                SimulatedDevice simulated = new SimulatedDevice(device.ToString());
                listDevices.Add(simulated);

                // create a new thread that will post content
                Task.Factory.StartNew(() =>
                {
                    DateTimeOffset dt = DateTimeOffset.Now;

                    while (true)
                    {
                        if (dt.AddSeconds(simulated.Interval) < DateTimeOffset.Now)
                        {
                            dt = DateTimeOffset.Now;
                            // send a message
                            if (simulated.LoRaDevice.IsJoined)
                            {

                                var header = GetSyncHeader(PhysicalIdentifier.PUSH_DATA);
                                // So far, generate a random for tmst
                                Random random = new Random();
                                gateway.rxpk.tmst = (uint)random.Next();
                                gateway.rxpk.size = 23;

                                gateway.rxpk.data = simulated.GetUnconfirmedDataUpMessage();
                                gateway.rxpk.time = DateTime.UtcNow.ToString("O");
                                var msg = "{\"rxpk\":[" + JsonConvert.SerializeObject(gateway.rxpk) + "]}";

                                var gat = Encoding.Default.GetBytes(msg);
                                byte[] data = new byte[header.Length + gat.Length];
                                Array.Copy(header, data, header.Length);
                                Array.Copy(gat, 0, data, header.Length, gat.Length);

                                UdpSendMessage(data).GetAwaiter().GetResult();
                            }
                            else
                            {
                                //create a join request


                            }

                        }
                    }
                });

            }




            // TODO: Need to start this in a thread
            await RunUdpListener();

        }

        public static async Task UdpSendMessage(byte[] messageToSend)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                await udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp.ToString(), remoteLoRaAggregatorPort); //, 1680); //

            }
        }

        public static byte[] GetSyncHeader(PhysicalIdentifier physical)
        {
            byte[] buff = new byte[12];
            // first is the protocole version
            buff[0] = 2;
            // Random token
            byte[] array = new byte[2];
            Random random = new Random();
            random.NextBytes(array);
            buff[1] = array[0];
            buff[2] = array[1];
            // PULL_DATA
            buff[3] = (byte)physical;
            // Then the MAC address
            for (int i = 0; i < 8; i++)
                buff[4 + i] = mac[i];
            return buff;
        }

        private static byte[] GetMacAddress()
        {
            string macAddresses = string.Empty;

            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus == OperationalStatus.Up)
                {
                    macAddresses = adapter.GetPhysicalAddress().ToString();
                    break;
                }
            }

            return Encoding.Default.GetBytes(macAddresses);
        }

        async Task RunUdpListener()
        {


            while (true)
            {
                UdpReceiveResult receivedResults = await udpClient.ReceiveAsync();

                //Logger.Log($"UDP message received ({receivedResults.Buffer.Length} bytes) from port: {receivedResults.RemoteEndPoint.Port}");


                // If 4, it may mean we received a confirmation
                if (receivedResults.Buffer.Length == 4)
                {
                    // TODO: check the last message sent tocken
                    if (receivedResults.Buffer[3] == (byte)PhysicalIdentifier.PUSH_ACK)
                    {
                        //Bingo
                        Logger.Log($"Confirmation receiveced", Logger.LoggingLevel.Always);
                    }
                }

                try
                {
                    // TODO: process the message not really implemented yet
                    MessageProcessor messageProcessor = new MessageProcessor();
                    _ = messageProcessor.processMessage(receivedResults.Buffer);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error processing the message {ex.Message}", Logger.LoggingLevel.Error);
                }
            }
        }

        public void Dispose()
        {

        }
    }
}
