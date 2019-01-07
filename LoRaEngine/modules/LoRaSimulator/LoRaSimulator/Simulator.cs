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
using LoRaTools.LoRaMessage;
using System.Threading;
using System.Linq;
using System.Diagnostics;
using LoRaTools.LoRaPhysical;

namespace LoRaSimulator
{
    public class Simulator : IDisposable
    {
        static private int PORT = 1681;

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

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIMULATOR_PORT")))
            {
                PORT = Convert.ToInt32(Environment.GetEnvironmentVariable("SIMULATOR_PORT"));
                Logger.Log($"Changing port to {PORT}", Logger.LoggingLevel.Always);
            }

            // Creating the endpoint
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            udpClient = new UdpClient(endPoint);

            Logger.Log($"LoRaWAN Simulator started on port {PORT}", Logger.LoggingLevel.Always);
            //send first sync
            _ = Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var sync = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PULL_DATA, null);
                    await UdpSendMessage(sync.GetSyncHeader(mac));
                    await Task.Delay(10000);
                }
            });
            // Reading the test configuration
            string json = System.IO.File.ReadAllText(@"testconfig.json");

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
                _ = Task.Factory.StartNew(async () =>
                {
                    simulated.dt = DateTimeOffset.Now;

                    while (true)
                    {
                        Random random = new Random();
                        var rand = (random.NextDouble() - 0.5) * simulated.RandomInterval;
                        if (simulated.dt.AddSeconds(simulated.Interval + rand) < DateTimeOffset.Now)
                        {
                            //simulated.dt = DateTimeOffset.Now;
                            // check if the device is part of a group
                            // if yes, then find all the devices and make the loop all together
                            // send all the messages together
                            simulated.dt = DateTimeOffset.Now;
                            List<SimulatedDevice> devgroup = new List<SimulatedDevice>();
                            if (simulated.GroupRxpk != 0)
                            {
                                devgroup = listDevices.Where(x => x.GroupRxpk == simulated.GroupRxpk).ToList();
                            }
                            else
                            {
                                devgroup.Add(simulated);
                            }
                            //Debug.WriteLine(JsonConvert.SerializeObject(devgroup));
                            var msg = "{\"rxpk\":[";
                            var devicetosend = "";

                            foreach (var simul in devgroup)
                            {
                                byte[] tosend;
                                simul.LastPayload = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PUSH_DATA, null);

                                if (simul.LoRaDevice.IsJoined)
                                {

                                    tosend = simul.GetUnconfirmedDataUpMessage();
                                }
                                else
                                {
                                    //simulated.LastPayload = new PhysicalPayload(GetRandomToken(), PhysicalIdentifier.PUSH_DATA, null);
                                    //var header = simulated.LastPayload.GetSyncHeader(mac);
                                    tosend = simul.GetJoinRequest();
                                    //    var rxpkgateway = gateway.GetMessage(tosend);
                                    //    var msg = "{\"rxpk\":[" + rxpkgateway + "]}";
                                    //    var gat = Encoding.Default.GetBytes(msg);
                                    //    byte[] data = new byte[header.Length + gat.Length];
                                    //    Array.Copy(header, data, header.Length);
                                    //    Array.Copy(gat, 0, data, header.Length, gat.Length);
                                    //    await UdpSendMessage(data);
                                }
                                var rxpkgateway = gateway.GetMessage(tosend);

                                msg += rxpkgateway + ",";
                                devicetosend += simul.LoRaDevice.DevEUI + ",";
                                simul.dt = DateTimeOffset.Now;
                            }
                            byte[] header = simulated.LastPayload.GetSyncHeader(mac);
                            // get rid of the the last ","
                            msg = msg.Substring(0, msg.Length - 1);
                            msg += "]}";
                            devicetosend = devicetosend.Substring(0, devicetosend.Length - 1);
                            var gat = Encoding.Default.GetBytes(msg);
                            byte[] data = new byte[header.Length + gat.Length];
                            Array.Copy(header, data, header.Length);
                            Array.Copy(gat, 0, data, header.Length, gat.Length);

                            await UdpSendMessage(data);
                            Logger.Log(devicetosend, $"Sending data: {BitConverter.ToString(header).Replace("-", "")}{Encoding.Default.GetString(gat)}", Logger.LoggingLevel.Always);
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
                await udpClient.SendAsync(messageToSend, messageToSend.Length, remoteLoRaAggregatorIp.ToString(), remoteLoRaAggregatorPort);

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

                // Logger.Log($"UDP message received ({receivedResults.Buffer.Length} bytes) from port: {receivedResults.RemoteEndPoint.Port}", Logger.LoggingLevel.Always);


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
                    try
                    {
                        foreach (var dev in listDevices)
                        {
                            if (dev.LastPayload != null)
                                if ((dev.LastPayload.token[0] == token[0]) && (dev.LastPayload.token[1] == token[1]))
                                {
                                    string device = dev.LoRaDevice.DevEUI;
                                    // check last operation and answer
                                    // Is is a simple push data?                                    
                                    if (identifier == PhysicalIdentifier.PUSH_ACK)
                                    {
                                        if (dev.LastPayload.identifier == PhysicalIdentifier.PUSH_DATA)
                                        {
                                            Logger.Log(device, $"PUSH_DATA confirmation receiveced from NetworkServer", Logger.LoggingLevel.Info);
                                        }
                                        else
                                        {
                                            Logger.Log(device, $"PUSH_ACK confirmation receiveced from ", Logger.LoggingLevel.Info);
                                        }
                                    }
                                    else if (identifier == PhysicalIdentifier.PULL_RESP)
                                    {
                                        // we asked something, we get an answer
                                        var txpk=Txpk.CreateTxpk(receivedResults.Buffer, dev.LoRaDevice.AppKey);
                                        LoRaMessageWrapper loraMessage = new LoRaMessageWrapper(txpk, dev.LoRaDevice.AppKey);
                                    
                                            // Check if the device is not joined, then it is maybe the answer
                                            if ((loraMessage.LoRaMessageType == LoRaMessageType.JoinAccept) && (dev.LoRaDevice.DevAddr == ""))
                                            {
                                                Logger.Log(device, $"Received join accept", Logger.LoggingLevel.Info);
                                                var payload = (LoRaPayloadJoinAccept)loraMessage.LoRaPayloadMessage;
                                                // TODO Need to check if the time is not passed 

                                                // Calculate the keys
                                                var netid = payload.NetID.ToArray();
                                                Array.Reverse(netid);
                                                var appNonce = payload.AppNonce.ToArray();
                                                Array.Reverse(appNonce);
                                                var devNonce = dev.LoRaDevice.GetDevNonce();
                                                Array.Reverse(devNonce);
                                                var appSKey = payload.CalculateKey(LoRaPayload.KeyType.AppSKey, appNonce, netid, devNonce, dev.LoRaDevice.GetAppKey());
                                                dev.LoRaDevice.AppSKey = BitConverter.ToString(appSKey).Replace("-", "");
                                                var nwkSKey = payload.CalculateKey(LoRaPayload.KeyType.NwkSKey, appNonce, netid, devNonce, dev.LoRaDevice.GetAppKey());
                                                dev.LoRaDevice.NwkSKey = BitConverter.ToString(nwkSKey).Replace("-", "");
                                                dev.LoRaDevice.NetId = BitConverter.ToString(netid).Replace("-", "");
                                                dev.LoRaDevice.AppNonce = BitConverter.ToString(appNonce).Replace("-", "");
                                                var devAdd = payload.DevAddr;
                                                //Array.Reverse(devAdd);
                                                dev.LoRaDevice.DevAddr = BitConverter.ToString(devAdd.ToArray()).Replace("-", "");
                                            }
                                        
                                    }
                                }
                        }
                    }
                    catch (Exception ex)
                    {

                        Logger.Log($"Something when wrong: {ex.Message}", Logger.LoggingLevel.Error);
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
