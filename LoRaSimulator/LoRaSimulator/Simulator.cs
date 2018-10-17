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
using PacketManager;

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

            var sync = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PUSH_DATA, null);
            await UdpSendMessage(sync.GetSyncHeader(mac));


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
                                simulated.LastPayload = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PUSH_DATA, null);
                                var header = simulated.LastPayload.GetSyncHeader(mac);

                                var simdata = simulated.GetUnconfirmedDataUpMessage();
                                var rxpkgateway = gateway.GetMessage(simdata);
                                var msg = "{\"rxpk\":[" + rxpkgateway + "]}";

                                var gat = Encoding.Default.GetBytes(msg);
                                byte[] data = new byte[header.Length + gat.Length];
                                Array.Copy(header, data, header.Length);
                                Array.Copy(gat, 0, data, header.Length, gat.Length);
                                Logger.Log(simulated.LoRaDevice.DevAddr, $"Sending data: {data}", Logger.LoggingLevel.Always);
                                UdpSendMessage(data).GetAwaiter().GetResult();
                            }
                            else
                            {
                                simulated.LastPayload = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PUSH_DATA, null);
                                var header = simulated.LastPayload.GetSyncHeader(mac);

                                var join = simulated.GetJoinRequest();
                                var rxpkgateway = gateway.GetMessage(join);
                                var msg = "{\"rxpk\":[" + rxpkgateway + "]}";

                                var gat = Encoding.Default.GetBytes(msg);
                                byte[] data = new byte[header.Length + gat.Length];
                                Array.Copy(header, data, header.Length);
                                Array.Copy(gat, 0, data, header.Length, gat.Length);

                                UdpSendMessage(data).GetAwaiter().GetResult();
                            }

                        }
                    }
                });

            }




            // TODO: Need to start this in a thread
            await RunUdpListener();

        }

        public static byte[] GetRandomToken()
        {
            byte[] token = new byte[2];
            Random random = new Random();
            random.NextBytes(token);
            return token;
        }

        public static async Task UdpSendMessage(byte[] messageToSend)
        {
            if (messageToSend != null && messageToSend.Length != 0)
            {
                await udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp.ToString(), remoteLoRaAggregatorPort); //, 1680); //

            }
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

                Logger.Log($"UDP message received ({receivedResults.Buffer.Length} bytes) from port: {receivedResults.RemoteEndPoint.Port}", Logger.LoggingLevel.Always);


                // If 4, it may mean we received a confirmation
                if (receivedResults.Buffer.Length >= 4)
                {
                    // get the token
                    byte[] token = new byte[2];
                    token[0] = receivedResults.Buffer[1];
                    token[1] = receivedResults.Buffer[2];
                    // identifier
                    var identifier = (PhysicalIdentifier)receivedResults.Buffer[3];
                    // Find the device
                    foreach (var dev in listDevices)
                    {
                        if (dev.LastPayload != null)
                            if ((dev.LastPayload.token[0] == token[0]) && (dev.LastPayload.token[1] == token[1]))
                            {
                                string device = "";
                                if (dev.LoRaDevice.DevEUI != "")
                                    device = dev.LoRaDevice.DevEUI;
                                else
                                    device = dev.LoRaDevice.DevAddr;
                                // check last operation and answer
                                // Is is a simple push data?
                                if ((dev.LastPayload.identifier == PhysicalIdentifier.PUSH_DATA) && (identifier == PhysicalIdentifier.PUSH_ACK))
                                {
                                    Logger.Log(device, $"Confirmation receiveced", Logger.LoggingLevel.Always);
                                }
                                if (identifier == PhysicalIdentifier.PULL_RESP)
                                {
                                    // we asked something, we get an answer
                                    LoRaMessage loraMessage = new LoRaMessage(receivedResults.Buffer, true, dev.LoRaDevice.AppKey);
                                    if (!loraMessage.IsLoRaMessage)
                                    {
                                        // udpMsgForPktForwarder = ProcessNonLoraMessage(loraMessage);
                                        Logger.Log(device, $"Received a non LoRa message", Logger.LoggingLevel.Always);
                                    }
                                    else
                                    {

                                        // Check if the device is not joined, then it is maybe the answer
                                        if ((loraMessage.LoRaMessageType == LoRaMessageType.JoinAccept) && (dev.LoRaDevice.DevAddr != ""))
                                        {
                                            Logger.Log(device, $"Received join accept", Logger.LoggingLevel.Always);
                                            var payload = (LoRaPayloadJoinAccept)loraMessage.PayloadMessage;
                                            // Calculate the keys
                                            var appSKey = payload.CalculateKey(new byte[1] { 0x01 }, payload.AppNonce, payload.NetID, dev.LoRaDevice.GetDevNonce(), dev.LoRaDevice.GetAppKey());
                                            dev.LoRaDevice.AppSKey = Encoding.Default.GetString(appSKey);
                                            var nwkSKey = payload.CalculateKey(new byte[1] { 0x02 }, payload.AppNonce, payload.NetID, dev.LoRaDevice.GetDevNonce(), dev.LoRaDevice.GetAppKey());
                                            dev.LoRaDevice.NwkSKey = Encoding.Default.GetString(nwkSKey);
                                            dev.LoRaDevice.NetId = BitConverter.ToString(payload.NetID).Replace("-", ""); ;
                                            dev.LoRaDevice.AppNonce = BitConverter.ToString(payload.AppNonce).Replace("-", "");
                                            dev.LoRaDevice.DevAddr = BitConverter.ToString(payload.DevAddr).Replace("-", "");
                                        }
                                    }
                                }
                            }
                    }
                    //if (receivedResults.Buffer[3] == (byte)PhysicalIdentifier.PUSH_ACK)
                    //{
                    //    //Bingo
                    //    Logger.Log($"Confirmation receiveced", Logger.LoggingLevel.Always);
                    //}
                }

                //try
                //{
                //    // TODO: process the message not really implemented yet
                //    MessageProcessor messageProcessor = new MessageProcessor();
                //    _ = messageProcessor.processMessage(receivedResults.Buffer);
                //}
                //catch (Exception ex)
                //{
                //    Logger.Log($"Error processing the message {ex.Message}", Logger.LoggingLevel.Error);
                //}
            }
        }

        public void Dispose()
        {

        }
    }
}
