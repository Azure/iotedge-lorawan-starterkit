//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using Xunit;
using System.Text;
using System.Linq;
using LoRaTools.LoRaMessage;


namespace LoRaWanTest
{

    /// <summary>
    /// all these test cases were inspired from https://github.com/brocaar/lora-app-server
    /// </summary>
    public class LoRaLibTest
    {
        [Fact]

        public void TestJoinAccept()
        {
            byte[] AppNonce = new byte[3]{
                87,11,199
            };
            byte[] NetId = new byte[3]{
                34,17,1
            };
            byte[] DevAddr = new byte[4]{
                2,3,25,128
            };
            var netId = BitConverter.ToString(NetId).Replace("-", "");
            LoRaPayloadJoinAccept joinAccept = new LoRaPayloadJoinAccept(netId, "00112233445566778899AABBCCDDEEFF", DevAddr, AppNonce);
            Console.WriteLine(BitConverter.ToString(joinAccept.GetByteMessage()));
            LoRaMessageWrapper joinAcceptMessage = new LoRaMessageWrapper(joinAccept, LoRaMessageType.JoinAccept, new byte[] { 0x01 });
            byte[] joinAcceptMic = new byte[4]{
                67, 72, 91, 188
                };

            Assert.True((((LoRaPayloadJoinAccept)joinAcceptMessage.PayloadMessage).Mic.ToArray().SequenceEqual(joinAcceptMic)));
           
            var msg = BitConverter.ToString(joinAcceptMessage.PayloadMessage.GetByteMessage()).Replace("-", String.Empty);
            Assert.Equal("20493EEB51FBA2116F810EDB3742975142", msg);

        }


        [Fact]
        public void TestJoinRequest()
        {
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            string jsonUplink = @"{ ""rxpk"":[
 	            {
		            ""time"":""2013-03-31T16:21:17.528002Z"",
 		            ""tmst"":3512348611,
 		            ""chan"":2,
 		            ""rfch"":0,
 		            ""freq"":866.349812,
 		            ""stat"":1,
 		            ""modu"":""LORA"",
 		            ""datr"":""SF7BW125"",
 		            ""codr"":""4/6"",
 		            ""rssi"":-35,
 		            ""lsnr"":5.1,
 		            ""size"":32,
 		            ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }]}";
            var joinRequestInput = Encoding.Default.GetBytes(jsonUplink);
            LoRaMessageWrapper joinRequestMessage = new LoRaMessageWrapper(physicalUpstreamPyld.Concat(joinRequestInput).ToArray());

            if (joinRequestMessage.LoRaMessageType != LoRaMessageType.JoinRequest)
                Console.WriteLine("Join Request type was not parsed correclty");
            byte[] joinRequestAppKey = new byte[16]
            { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1};
            var joinRequestBool = joinRequestMessage.CheckMic(BitConverter.ToString(joinRequestAppKey).Replace("-", ""));
            if (!joinRequestBool)
            {
                Console.WriteLine("Join Request type was not computed correclty");
            }

            byte[] joinRequestAppEui = new byte[8]
            {1, 2, 3, 4, 1, 2, 3, 4};

            byte[] joinRequestDevEUI = new byte[8]
           {2, 3, 4, 5, 2, 3, 4, 5};
            byte[] joinRequestDevNonce = new byte[2]
            {16,45};

            Array.Reverse(joinRequestAppEui);
            Array.Reverse(joinRequestDevEUI);
            Array.Reverse(joinRequestDevNonce);
            Assert.True(joinRequestMessage.PayloadMessage.GetLoRaMessage().AppEUI.ToArray().SequenceEqual(joinRequestAppEui));
            Assert.True(joinRequestMessage.PayloadMessage.GetLoRaMessage().DevEUI.ToArray().SequenceEqual(joinRequestDevEUI));
            Assert.True(joinRequestMessage.PayloadMessage.GetLoRaMessage().DevNonce.ToArray().SequenceEqual(joinRequestDevNonce));


        }

        [Fact]
        public void TestUnconfirmedUplink()
        {
            string jsonUplinkUnconfirmedDataUp = @"{ ""rxpk"":[
               {
               ""time"":""2013-03-31T16:21:17.528002Z"",
                ""tmst"":3512348611,
                ""chan"":2,
                ""rfch"":0,
                ""freq"":866.349812,
                ""stat"":1,
                ""modu"":""LORA"",
                ""datr"":""SF7BW125"",
                ""codr"":""4/6"",
                ""rssi"":-35,
                ""lsnr"":5.1,
                ""size"":32,
                ""data"":""QAQDAgGAAQABppRkJhXWw7WC""
                 }]}";

            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;

            var jsonUplinkUnconfirmedDataUpBytes = Encoding.Default.GetBytes(jsonUplinkUnconfirmedDataUp);
            LoRaMessageWrapper jsonUplinkUnconfirmedMessage = new LoRaMessageWrapper(physicalUpstreamPyld.Concat(jsonUplinkUnconfirmedDataUpBytes).ToArray());
            Assert.Equal(LoRaMessageType.UnconfirmedDataUp, jsonUplinkUnconfirmedMessage.LoRaMessageType);

            LoRaPayloadStandardData loRaPayloadUplinkObj = (LoRaPayloadStandardData)jsonUplinkUnconfirmedMessage.PayloadMessage;


            Assert.True(loRaPayloadUplinkObj.Fcnt.Span.SequenceEqual(new byte[2] { 1, 0 }));


            Assert.True(loRaPayloadUplinkObj.DevAddr.SequenceEqual(new byte[4] { 1, 2, 3, 4 }));
            byte[] LoRaPayloadUplinkNwkKey = new byte[16] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 };


            Assert.True(loRaPayloadUplinkObj.CheckMic(BitConverter.ToString(LoRaPayloadUplinkNwkKey).Replace("-", "")));

            byte[] LoRaPayloadUplinkAppKey = new byte[16] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            var key = BitConverter.ToString(LoRaPayloadUplinkAppKey).Replace("-", "");
            Assert.Equal("hello",Encoding.ASCII.GetString((loRaPayloadUplinkObj.PerformEncryption(key))));

        }

        [Fact]
        public void TestConfirmedDataUp()
        {
            byte[] mhdr = new byte[1];
            mhdr[0] = 128;
            byte[] devAddr = new byte[4]
                {4,3,2,1
                };

            byte[] fctrl = new byte[1]{
                0 };
            byte[] fcnt = new byte[2]
            {0,0 };
            byte[] fport = new byte[1]
            {
                    10
            };
            byte[] frmPayload = new byte[4]
            {
                4,3,2,1
            };

            var appkey = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            var nwkkey = new byte[16] { 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };
            Array.Reverse(appkey);
            Array.Reverse(nwkkey);

            LoRaPayloadStandardData lora = new LoRaPayloadStandardData(mhdr, devAddr, fctrl, fcnt, null, fport, frmPayload, 0);
            lora.PerformEncryption(BitConverter.ToString(appkey).Replace("-", ""));
            byte[] testEncrypt = new byte[4]
            {
                226, 100, 212, 247
            };
            Assert.Equal(testEncrypt, lora.Frmpayload.ToArray());
            lora.SetMic(BitConverter.ToString(nwkkey).Replace("-", ""));
            byte[] testMic = new byte[4]
            {
                181, 106, 14, 117
            };
            Assert.Equal(testMic, lora.Mic.ToArray());
            var mess = lora.GetByteMessage();
            //TODO

        }

        [Fact]
        public void TestKeys()
        {

            //create random message
            string jsonUplink = @"{ ""rxpk"":[
 	            {
		            ""time"":""2013-03-31T16:21:17.528002Z"",
 		            ""tmst"":3512348611,
 		            ""chan"":2,
 		            ""rfch"":0,
 		            ""freq"":866.349812,
 		            ""stat"":1,
 		            ""modu"":""LORA"",
 		            ""datr"":""SF7BW125"",
 		            ""codr"":""4/6"",
 		            ""rssi"":-35,
 		            ""lsnr"":5.1,
 		            ""size"":32,
 		            ""data"":""AAQDAgEEAwIBBQQDAgUEAwItEGqZDhI=""
                }]}";
            var joinRequestInput = Encoding.Default.GetBytes(jsonUplink);
            byte[] physicalUpstreamPyld = new byte[12];
            physicalUpstreamPyld[0] = 2;
            LoRaMessageWrapper joinRequestMessage = new LoRaMessageWrapper(physicalUpstreamPyld.Concat(joinRequestInput).ToArray());

            var joinReq = (LoRaPayloadJoinRequest)joinRequestMessage.PayloadMessage;
            joinReq.DevAddr = new byte[4]
            {
                4,3,2,1
            };
            joinReq.DevEUI = new byte[8]
            {
                8,7,6,5,4,3,2,1
            };
            joinReq.DevNonce = new byte[2]
            {
                2,1
            };
            joinReq.AppEUI = new byte[8]
            {
                1,2,3,4,5,6,7,8
            };

            byte[] appNonce = new byte[3]
            {
                0,0,1
            };
            byte[] netId = new byte[3]
            {
                3,2,1
            };
            byte[] appKey = new byte[16]
            {
                1, 2, 3, 4, 5, 6, 7, 8, 1, 2, 3, 4, 5, 6, 7, 8
            };
            var key = joinReq.CalculateKey(new byte[1] { 0x01 }, appNonce, netId, joinReq.DevNonce.ToArray(), appKey);
            Assert.Equal(key, new byte[16]{
                223, 83, 195, 95, 48, 52, 204, 206, 208, 255, 53, 76, 112, 222, 4, 223
            }
            );
        }

        [Fact]
        public void test()
        {
            string jsonUplink = @"{ ""rxpk"":[
 	            {
		            ""time"":""2013-03-31T16:21:17.528002Z"",
 		            ""tmst"":3512348611,
 		            ""chan"":2,
 		            ""rfch"":0,
 		            ""freq"":866.349812,
 		            ""stat"":1,
 		            ""modu"":""LORA"",
 		            ""datr"":""SF7BW125"",
 		            ""codr"":""4/6"",
 		            ""rssi"":-35,
 		            ""lsnr"":5.1,
 		            ""size"":32,
 		            ""data"":""IBMeputKQRfOUiGYyaskCt4=""
                }]}";
            var joinRequestInput = Encoding.Default.GetBytes(jsonUplink);
            LoRaMessageWrapper joinRequestMessage = new LoRaMessageWrapper(joinRequestInput.Concat(joinRequestInput).ToArray());

        }

    }
}
