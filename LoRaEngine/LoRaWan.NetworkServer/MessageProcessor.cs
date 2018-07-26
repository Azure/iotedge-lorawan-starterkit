using Newtonsoft.Json.Linq;
using PacketManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LoRaWan.NetworkServer
{
    public class MessageProcessor : IDisposable
    {
        //string testKey = "2B7E151628AED2A6ABF7158809CF4F3C";
        //string testDeviceId = "BE7A00000000888F";
        private static UInt16 counter=1;

        public async Task processMessage(byte[] message)
        {
            LoRaMessage loraMessage = new LoRaMessage(message);

            byte[] messageToSend = new Byte[0];

            if (!loraMessage.isLoRaMessage)
            {
                messageToSend = ProcessNonLoraMessage(loraMessage);
            }
            else
            {
                //join message
                if (loraMessage.loRaMessageType == LoRaMessageType.JoinRequest)
                {
                    messageToSend = await ProcessJoinRequest(loraMessage);

                }
                //normal message
                else if( loraMessage.loRaMessageType ==LoRaMessageType.UnconfirmedDataUp)
                {
                    messageToSend = await ProcessLoraMessage(loraMessage);

                }else if (loraMessage.loRaMessageType == LoRaMessageType.ConfirmedDataUp)
                {
                    messageToSend = await ProcessLoraMessage(loraMessage);


                    var _datr = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].datr;

                    uint _rfch = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].rfch;

                    double _freq = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].freq;

                    long _tmst = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].tmst;

                    Byte[] devAddrCorrect = new byte[4];
                    Array.Copy(loraMessage.payloadMessage.devAddr, devAddrCorrect, 4);
                    Array.Reverse(devAddrCorrect);

                    LoRaPayloadStandardData ackLoRaMessage = new LoRaPayloadStandardData(StringToByteArray("A0"),
                        devAddrCorrect,
                         new byte[1] { 32 },
                         BitConverter.GetBytes(counter)
                         ,    
                        null,
                        null,
                        null,
                        1);

                    counter++;

                    //todo ronnie
                    string devAddr = BitConverter.ToString(devAddrCorrect).Replace("-", "");

                    Console.WriteLine($"Processing message from device: {devAddr}");

                    Shared.loraDeviceInfoList.TryGetValue(devAddr, out LoraDeviceInfo loraDeviceInfo);

                    ackLoRaMessage.PerformEncryption(loraDeviceInfo.AppSKey);
                    ackLoRaMessage.SetMic(loraDeviceInfo.NwkSKey);


                    byte[] rndToken = new byte[2];
                    Random rnd = new Random();
                    rnd.NextBytes(rndToken);
                    LoRaMessage ackMessage = new LoRaMessage(ackLoRaMessage, LoRaMessageType.ConfirmedDataDown, rndToken, _datr, 0, _freq, _tmst);
                   
                    messageToSend = ackMessage.physicalPayload.GetMessage();
                    
                }
                else
                {

                }

            }
          

            //send reply to pktforwarder
            await UdpServer.UdpSendMessage(messageToSend);
        }

        private static byte[] ProcessNonLoraMessage(LoRaMessage loraMessage)
        {
            byte[] messageToSend = new byte[0];
            if (loraMessage.physicalPayload.identifier == PhysicalIdentifier.PULL_DATA)
            {

                PhysicalPayload pullAck = new PhysicalPayload(loraMessage.physicalPayload.token, PhysicalIdentifier.PULL_ACK, null);

                messageToSend = pullAck.GetMessage();

                Console.WriteLine("Pull Ack sent");

            }


                return messageToSend;
        }
        private async static Task<byte[]> ProcessLoraMessage(LoRaMessage loraMessage)
        {
            byte[] messageToSend = new byte[0];
            string devAddr = BitConverter.ToString(loraMessage.payloadMessage.devAddr).Replace("-", "");

            Console.WriteLine($"Processing message from device: {devAddr}");

            Shared.loraDeviceInfoList.TryGetValue(devAddr, out LoraDeviceInfo loraDeviceInfo);


            if (loraDeviceInfo == null)
            {
                Console.WriteLine("No cache");

                loraDeviceInfo = await LoraDeviceInfoManager.GetLoraDeviceInfoAsync(devAddr);

                Shared.loraDeviceInfoList.TryAdd(devAddr, loraDeviceInfo);
            }
            else
            {
                Console.WriteLine("From cache");
            }


            if (loraDeviceInfo.IsOurDevice)
            {

                if (loraMessage.CheckMic(loraDeviceInfo.NwkSKey))
                {
                    string decryptedMessage = null;
                    try
                    {
                        decryptedMessage = loraMessage.DecryptPayload(loraDeviceInfo.AppSKey);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to decrypt message: {ex.Message}");
                    }



                    PhysicalPayload pushAck = new PhysicalPayload(loraMessage.physicalPayload.token, PhysicalIdentifier.PUSH_ACK, null);

                    messageToSend = pushAck.GetMessage();

                    Console.WriteLine($"Sending message '{decryptedMessage}' to hub...");

                    if (loraDeviceInfo.HubSender == null)
                    {

                        loraDeviceInfo.HubSender = new IoTHubSender(loraDeviceInfo.DevEUI, loraDeviceInfo.PrimaryKey);

                    }


                    await loraDeviceInfo.HubSender.SendMessage(decryptedMessage);

                }
                else
                {
                    Console.WriteLine("Check MIC failed! Device will be ignored from now on...");
                    loraDeviceInfo.IsOurDevice = false;
                }

            }
            else
            {
                Console.WriteLine($"Ignore message because is not our device");
            }

            return messageToSend;
        }
        private async Task<byte[]> ProcessJoinRequest(LoRaMessage loraMessage)
        {
            Console.WriteLine("Join Request Received");

            byte[] messageToSend = new Byte[0];

            LoraDeviceInfo joinLoraDeviceInfo;

            var joinReq = (LoRaPayloadJoinRequest)loraMessage.payloadMessage;

            Array.Reverse(joinReq.devEUI);
            Array.Reverse(joinReq.appEUI);

            string devEui = BitConverter.ToString(joinReq.devEUI).Replace("-", "");
            string devNonce = BitConverter.ToString(joinReq.devNonce).Replace("-", "");

            //checking if this devnonce was already processed or the deveui was already refused
            Shared.loraJoinRequestList.TryGetValue(devEui, out joinLoraDeviceInfo);


            //we have a join request in the cache
            if (joinLoraDeviceInfo != null)
            {

                //it is not our device so ingore the join
                if (!joinLoraDeviceInfo.IsOurDevice)
                {
                    Console.WriteLine("Join Request refused the device is not ours");
                    return null;
                }
                //is our device but the join was not valid
                else if (!joinLoraDeviceInfo.IsJoinValid)
                {
                    //if the devNonce is equal to the current it is a potential replay attck
                    if (joinLoraDeviceInfo.DevNonce == devNonce)
                    {
                        Console.WriteLine("Join Request refused devNonce already used");
                        return null;
                    }
                }

            }

            

            joinLoraDeviceInfo = await LoraDeviceInfoManager.PerformOTAAAsync(devEui, BitConverter.ToString(joinReq.appEUI).Replace("-", ""), devNonce);

            if (joinLoraDeviceInfo.IsJoinValid)
            {

                byte[] appNonce = StringToByteArray(joinLoraDeviceInfo.AppNonce);

                byte[] netId = StringToByteArray(joinLoraDeviceInfo.NetId);

               

                byte[] devAddr = StringToByteArray(joinLoraDeviceInfo.DevAddr);

                string appKey = joinLoraDeviceInfo.AppKey;

                Array.Reverse(netId);
                Array.Reverse(appNonce);

                LoRaPayloadJoinAccept loRaPayloadJoinAccept = new LoRaPayloadJoinAccept(
                    //NETID 0 / 1 is default test 
                    BitConverter.ToString(netId).Replace("-", ""),
                    //todo add app key management
                    appKey,
                    //todo add device address management
                    devAddr,
                    appNonce
                    );

                var _datr = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].datr;

                uint _rfch = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].rfch;

                double _freq = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].freq;

                long _tmst = ((UplinkPktFwdMessage)loraMessage.loraMetadata.fullPayload).rxpk[0].tmst;

                LoRaMessage joinAcceptMessage = new LoRaMessage(loRaPayloadJoinAccept, LoRaMessageType.JoinAccept, loraMessage.physicalPayload.token, _datr, 0, _freq, _tmst);

                messageToSend = joinAcceptMessage.physicalPayload.GetMessage();

                //add to cache for processing normal messages. This awioids one additional call to the server.
                Shared.loraDeviceInfoList.TryAdd(joinLoraDeviceInfo.DevAddr, joinLoraDeviceInfo);

                Console.WriteLine("Join Accept sent");
                  
             }

            //add to cache to avoid replay attack, btw server side does the check too.
            Shared.loraJoinRequestList.TryAdd(devEui, joinLoraDeviceInfo);

            return messageToSend;
        }

        private byte[] StringToByteArray(string hex)
        {

            return Enumerable.Range(0, hex.Length)

                             .Where(x => x % 2 == 0)

                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))

                             .ToArray();

        }

        public void Dispose()
        {

        }
    }
}
