using System;
using System.Text;
using PacketManager;
using System.Linq;
using Newtonsoft.Json;
using LoRaTools;
using System.Net.Sockets;
using System.Net;
using LoRaWan.NetworkServer;

namespace AESDemo
{


    class Program
    {

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }
        static Program()
        {
        }
        static void Main(string[] args)
        {
            //Section testing different kind of decryption.
            byte[] leadingByte = StringToByteArray("0205DB00AA555A0000000101");

            //string inputJson = "{\"rxpk\":[{\"tmst\":3121882787,\"chan\":2,\"rfch\":1,\"freq\":868.500000,\"stat\":1,\"modu\":\"LORA\",\"datr\":\"SF7BW125\",\"codr\":\"4/5\",\"lsnr\":7.0,\"rssi\":-16,\"size\":20,\"data\":\"QEa5KACANwAIXiRAODD6gSCHMSk=\"}]}";

            //byte[] messageraw = leadingByte.Concat(Encoding.Default.GetBytes(inputJson)).ToArray();
            //LoRaMessage message = new LoRaMessage(messageraw);
            //Console.WriteLine("decrypted " + (message.DecryptPayload("2B7E151628AED2A6ABF7158809CF4F3C")));
            //Console.WriteLine("mic is valid: " + message.CheckMic("2B7E151628AED2A6ABF7158809CF4F3C"));

            //// join message
            //string joinInputJson = "{\"rxpk\":[{\"tmst\":286781788,\"chan\":0,\"rfch\":1,\"freq\":868.100000,\"stat\":1,\"modu\":\"LORA\",\"datr\":\"SF12BW125\",\"codr\":\"4/5\",\"lsnr\":11.0,\"rssi\":-17,\"size\":23,\"data\":\"AEZIZ25pc2lSj4gAAAAAer5VEV5aL4c=\"}]}";

            ////byte[] joinBytes = StringToByteArray("00DC0000D07ED5B3701E6FEDF57CEEAF00C886030AF2C9");

            //byte[] messageJoinraw = leadingByte.Concat(Encoding.Default.GetBytes(joinInputJson)).ToArray();
            //LoRaMessage joinMessage = new LoRaMessage(messageJoinraw);
            ////Console.WriteLine("decrypted " + (joinMessage.DecryptPayload("2B7E151628AED2A6ABF7158809CF4F3C")));
            //Console.WriteLine("mic is valid: " + joinMessage.CheckMic("2B7E151628AED2A6ABF7158809CF4F3C"));

            ////Section running the server to monitor LoRaWan messages, working for upling msg
            //UdpServer udp = new UdpServer();
            // udp.RunServer(true);
            //Console.Read();


            //section testing correct parsing of Join Request
            // byte[] physicalUpstreamPyld = new byte[12];
            // physicalUpstreamPyld[0] = 2;
            // string jsonUplink = @"{ ""rxpk"":[
            //   {
            //   ""time"":""2013-03-31T16:21:17.528002Z"",
            //    ""tmst"":3512348611,
            //    ""chan"":2,
            //    ""rfch"":0,
            //    ""freq"":866.349812,
            //    ""stat"":1,
            //    ""modu"":""LORA"",
            //    ""datr"":""SF7BW125"",
            //    ""codr"":""4/6"",
            //    ""rssi"":-35,
            //    ""lsnr"":5.1,
            //    ""size"":32,
            //    ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
            //     }]}";
            // var joinRequestInput = Encoding.Default.GetBytes(jsonUplink);
            // LoRaMessage joinRequestMessage = new LoRaMessage(physicalUpstreamPyld.Concat(joinRequestInput).ToArray());

            // if (joinRequestMessage.loRaMessageType != LoRaMessageType.JoinRequest)
            //     Console.WriteLine("Join Request type was not parsed correclty");
            // byte[] joinRequestAppKey = new byte[16]
            // { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1};
            // var joinRequestBool = joinRequestMessage.CheckMic(BitConverter.ToString(joinRequestAppKey).Replace("-", ""));
            // if (!joinRequestBool)
            // {
            //     Console.WriteLine("Join Request type was not computed correclty");
            // }

            // byte[] joinRequestAppEui = new byte[8]
            // {1, 2, 3, 4, 1, 2, 3, 4};

            // byte[] joinRequestDevEUI = new byte[8]
            //{2, 3, 4, 5, 2, 3, 4, 5};
            // byte[] joinRequestDevNonce = new byte[2]
            // {16,45};

            // Array.Reverse(joinRequestAppEui);
            // Array.Reverse(joinRequestDevEUI);
            // Array.Reverse(joinRequestDevNonce);
            // LoRaPayloadJoinRequest joinRequestMessagePayload = ((LoRaPayloadJoinRequest)joinRequestMessage.payloadMessage);

            // if(!joinRequestMessagePayload.appEUI.SequenceEqual(joinRequestAppEui))
            // {
            //     Console.WriteLine("Join Request appEUI was not computed correclty");
            // }
            // if (!joinRequestMessagePayload.devEUI.SequenceEqual(joinRequestDevEUI))
            // {
            //     Console.WriteLine("Join Request devEUI was not computed correclty");
            // }
            // if (!joinRequestMessagePayload.devNonce.SequenceEqual(joinRequestDevNonce))
            // {
            //     Console.WriteLine("Join Request devNonce was not computed correclty");
            // }

            // //Section testing correct build up of a Join Accept
            // byte[] AppNonce = new byte[3]{
            //     87,11,199
            // };
            // byte[] NetId = new byte[3]{
            //     34,17,1
            // };
            // byte[] DevAddr = new byte[4]{
            //     2,3,25,128
            // };
            // var netId = BitConverter.ToString(NetId).Replace("-", "");
            // LoRaPayloadJoinAccept joinAccept = new LoRaPayloadJoinAccept(netId, "00112233445566778899AABBCCDDEEFF", DevAddr, AppNonce);
            // Console.WriteLine(BitConverter.ToString(joinAccept.ToMessage()));
            // LoRaMessage joinAcceptMessage = new LoRaMessage(joinAccept, LoRaMessageType.JoinAccept, new byte[] { 0x01 });
            // byte[] joinAcceptMic = new byte[4]{
            //     67, 72, 91, 188
            //     };
            // if (!((LoRaPayloadJoinAccept)joinAcceptMessage.payloadMessage).mic.SequenceEqual(joinAcceptMic))
            // {
            //     Console.WriteLine("Join Accept Mic was not computed correclty");

            // }
            // var msg = BitConverter.ToString(((LoRaPayloadJoinAccept)joinAcceptMessage.payloadMessage).ToMessage()).Replace("-", String.Empty);
            // if (msg.CompareTo( "20493EEB51FBA2116F810EDB3742975142")!=0)
            //     Console.WriteLine("Join Accept encryption was not computed correclty");


            //string jsonUplinkUnconfirmedDataUp = @"{ ""rxpk"":[
            //   {
            //   ""time"":""2013-03-31T16:21:17.528002Z"",
            //    ""tmst"":3512348611,
            //    ""chan"":2,
            //    ""rfch"":0,
            //    ""freq"":866.349812,
            //    ""stat"":1,
            //    ""modu"":""LORA"",
            //    ""datr"":""SF7BW125"",
            //    ""codr"":""4/6"",
            //    ""rssi"":-35,
            //    ""lsnr"":5.1,
            //    ""size"":32,
            //    ""data"":""QAQDAgGAAQABppRkJhXWw7WC""
            //     }]}";

            //byte[] physicalUpstreamPyld = new byte[12];
            //physicalUpstreamPyld[0] = 2;

            //var jsonUplinkUnconfirmedDataUpBytes = Encoding.Default.GetBytes(jsonUplinkUnconfirmedDataUp);
            //LoRaMessage jsonUplinkUnconfirmedMessage = new LoRaMessage(physicalUpstreamPyld.Concat(jsonUplinkUnconfirmedDataUpBytes).ToArray());
            //if (jsonUplinkUnconfirmedMessage.loRaMessageType != LoRaMessageType.UnconfirmedDataUp)
            //{
            //    Console.Write("Nok");
            //}
            //LoRaPayloadDataUp loRaPayloadUplinkObj = (LoRaPayloadDataUp) jsonUplinkUnconfirmedMessage.payloadMessage;
            //if(loRaPayloadUplinkObj.fcnt.SequenceEqual(new byte[2] { 0,1 }))
            //{
            //    Console.Write("Nok");

            //}

            //if (loRaPayloadUplinkObj.devAddr.SequenceEqual(new byte[4] { 4,3,2,1 }))
            //{
            //    Console.Write("Nok");

            //}

            //byte[] appSKey = new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            //var key = BitConverter.ToString(appSKey).Replace("-","");
            //if (loRaPayloadUplinkObj.DecryptPayload(key).CompareTo("hello")!=0)
            //{
            //    Console.Write("Nok");
            //}

            //byte[] networkSKey = new byte[16] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2};
            //var key2 = BitConverter.ToString(networkSKey).Replace("-", "");

            //if (loRaPayloadUplinkObj.CheckMic(key2)!= true)
            //{
            //    Console.Write("Nok");
            //}


        }
    }
}
